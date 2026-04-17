using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ProceduralSFXCompanion.Controls;
using ProceduralSFXCompanion.Models;
using ProceduralSFXCompanion.Services;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using Microsoft.Data.Sqlite;
using Dapper;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.Input;
using ProceduralSFXCompanion.Collections;
using ProceduralSFXCompanion.Utilities;

namespace ProceduralSFXCompanion.ViewModels;


public partial class SearchViewModel : ViewModelBase
{
    public FolderSelectorViewModel FolderSelectorViewModel { get; }
    private readonly string _dbConn;
    private readonly ISukiToastManager _toastManager;
    private readonly AudioService _audioService;
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
        
    public bool IsBusy
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public BulkObservableCollection<GraphDescription> SearchedEntries { get; set; }
    
    public SearchViewModel(AppSettingsService appSettingsService, ISukiToastManager toastManager, AudioService audioService)
    {
        SearchedEntries = new BulkObservableCollection<GraphDescription>();
        _toastManager = toastManager;
        _audioService = audioService;
        FolderSelectorViewModel = new FolderSelectorViewModel(appSettingsService, appSettingsService.AppSettings.FolderPaths, toastManager);
        FolderSelectorViewModel.OnUserAddedFolder += (s, e) =>  { _ = OnUserAddedFolder(e); };
        _dbConn = "Data Source=" + Path.Combine(appSettingsService.SettingsFolderPath, "SearchIndex.db");
        _ = SyncFoldersAsync();
    }

    private async Task OnUserAddedFolder(FolderItem folderItem)
    {
        var folder = folderItem.Path;
        if (!Directory.Exists(folder))
            return;

        await using var connection = new SqliteConnection(_dbConn);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();
        
        try
        {
            IsBusy = true;
            // Delete if has before adding to make sure all entries are up to date
            _ = await connection.ExecuteAsync("DELETE FROM GraphIndex WHERE FolderPath = @Path",
                folderItem, transaction);
            
            StartWatching(folder);
            var files = Directory.GetFiles(folder, "*.txt", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var content = await FileUtilities.ReadFirstSentenceAsync(file);
                if(String.IsNullOrEmpty(content))
                    continue;
                
                var lastWrite = File.GetLastWriteTimeUtc(file).Ticks;
                var item = new GraphDescription
                    { FileName = file, FolderPath = folder, Content = content, LastModified = lastWrite };
                await connection.ExecuteAsync(@"INSERT INTO GraphIndex (FileName, FolderPath, Content, LastModified)
                                                    VALUES (@FileName, @FolderPath, @Content, @LastModified)", item, transaction);
            }
            transaction.Commit();
            
            await connection.ExecuteAsync("VACUUM");
            await connection.ExecuteAsync("PRAGMA optimize");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _toastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("Failed to update database!")
                .WithContent($"{ex.Message}!")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Dismiss().ByClicking()
                .Queue();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SyncFoldersAsync()
    {
        IsBusy = true;
        try
        {
            await using var connection = new SqliteConnection(_dbConn);
            await connection.OpenAsync();
            
            await connection.ExecuteAsync(@"CREATE VIRTUAL TABLE IF NOT EXISTS GraphIndex USING fts5(
                                                FileName UNINDEXED, 
                                                FolderPath UNINDEXED, 
                                                Content, 
                                                LastModified UNINDEXED,
                                                tokenize = 'porter unicode61'
                                              );");

            var existingInDbQuery = await connection.QueryAsync<(string FileName, long LastModified)>(
                "SELECT FileName, LastModified FROM GraphIndex");
            var existingInDb = existingInDbQuery.ToDictionary(x => x.FileName, x => x.LastModified);
            var foundFiles = new HashSet<string>();
            var filesToUpdate = new List<GraphDescription>();

            foreach (var folderItem in FolderSelectorViewModel.FolderItems)
            {
                var folder = folderItem.Path;
                if (!Directory.Exists(folder))
                    continue;

                StartWatching(folder);
                var files = Directory.GetFiles(folder, "*.txt", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    foundFiles.Add(file);
                    var lastWrite = File.GetLastWriteTimeUtc(file).Ticks;

                    if (!existingInDb.TryGetValue(file, out long dbTime) || lastWrite > dbTime)
                    {
                        var content = await FileUtilities.ReadFirstSentenceAsync(file);
                        if (!String.IsNullOrEmpty(content))
                        {
                            filesToUpdate.Add(new GraphDescription {
                                FileName = file, FolderPath = folder,
                                Content = content, LastModified = lastWrite
                            });
                        }
                    }
                }
            }

            await using var transaction = connection.BeginTransaction();
            var filesToRemove = existingInDb.Keys.Where(f => !foundFiles.Contains(f)).ToList();
            if (filesToRemove.Any())
            {
                await connection.ExecuteAsync(
                    "DELETE FROM GraphIndex WHERE FileName = @Path",
                    filesToRemove.Select(f => new { Path = f }),
                    transaction
                );
            }

            foreach (var item in filesToUpdate)
            {
                await connection.ExecuteAsync(
                    "DELETE FROM GraphIndex WHERE FileName = @FileName",
                    new { item.FileName },
                    transaction
                );
                await connection.ExecuteAsync(@"
            INSERT INTO GraphIndex (FileName, FolderPath, Content, LastModified)
            VALUES (@FileName, @FolderPath, @Content, @LastModified)",
                    item,
                    transaction
                );
            }
            transaction.Commit();
            
            if (filesToRemove.Count > 0)
                await connection.ExecuteAsync("VACUUM", transaction);
            if (filesToUpdate.Count > 0)
                await connection.ExecuteAsync("PRAGMA optimize", transaction);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("Failed to init database!")
                .WithContent($"{ex.Message}!")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Dismiss().ByClicking()
                .Queue();
        }
        finally
        { 
            IsBusy = false;   
        }
    }
    
    public void DeleteEntry(string fileName)
    {
        using var connection = new SqliteConnection(_dbConn);
        connection.Open();
        connection.Execute("DELETE FROM GraphIndex WHERE FileName = @fileName", new { fileName });
    }

    public void DeleteEntriesByFolder(string folderPath)
    {
        using var connection = new SqliteConnection(_dbConn);
        connection.Open();
        connection.Execute("DELETE FROM GraphIndex WHERE FolderPath = @folderPath", new { folderPath });
    }
    
    private void StartWatching(string path)
    {
        try
        {
            if(_watchers.ContainsKey(path))
                return;
            
            var watcher = new FileSystemWatcher(path, "*.txt")
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };

            watcher.Changed += async (s, e) => await HandleUpdate(e.FullPath, path);
            watcher.Created += async (s, e) => await HandleUpdate(e.FullPath, path);
            watcher.Deleted += (s, e) => DeleteEntry(e.FullPath);
            watcher.Renamed += async (s, e) => { DeleteEntry(e.OldFullPath); 
                                                                      await HandleUpdate(e.FullPath, path); };
            _watchers.Add(path, watcher);
        }
        catch (Exception e)
        {
            _toastManager.CreateToast()
                         .OfType(NotificationType.Error)
                         .WithTitle("Can't watch file changes!")
                         .WithContent($"{e.Message}!")
                         .Dismiss().After(TimeSpan.FromSeconds(2))
                         .Dismiss().ByClicking()
                         .Queue();
        }
    }
    
    private async Task HandleUpdate(string path, string root)
    {
        // Small delay to ensure file is released by the OS/Editor
        await Task.Delay(500); 
        var content = await FileUtilities.ReadFirstSentenceAsync(path);
        if (!String.IsNullOrEmpty(content))
        {
            UpdateEntry(new GraphDescription
            {
                FileName = path,
                FolderPath = root,
                Content = content,
                LastModified = File.GetLastWriteTimeUtc(path).Ticks
            });
        }
    }
    
    private void UpdateEntry(GraphDescription entry)
    {
        using var connection = new SqliteConnection(_dbConn);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        
        try
        {
            var existing = connection.QueryFirstOrDefault<long?>(
                "SELECT LastModified FROM GraphIndex WHERE FileName = @FileName",
                new { entry.FileName }, transaction);
            
            if (existing == null || entry.LastModified > existing.Value)
            {
                connection.Execute(
                    "DELETE FROM GraphIndex WHERE FileName = @FileName",
                    new { entry.FileName },
                    transaction);

                connection.Execute(@"
                        INSERT INTO GraphIndex (FileName, FolderPath, Content, LastModified)
                        VALUES (@FileName, @FolderPath, @Content, @LastModified)",
                    entry,
                    transaction);
            }
            transaction.Commit();
        }
        catch (Exception e)
        {
            _toastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("Unable to Update Graph Index!")
                .WithContent($"{e.Message}!")
                .Dismiss().After(TimeSpan.FromSeconds(2))
                .Dismiss().ByClicking()
                .Queue();
            
            transaction.Rollback();
        }
    }
    
    [RelayCommand]
    public async Task SearchDescriptions(string searchTerm)
    {
        if (String.IsNullOrWhiteSpace(searchTerm))
            return;
        
        string cleanSearch = Regex.Replace(searchTerm, @"[^a-zA-Z0-9\s]", " ").Trim();
        if (String.IsNullOrWhiteSpace(cleanSearch))
            return;
        
        IsBusy = true;
        try
        {
            var terms = cleanSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(term =>
            {
                return $"\"{term}\"*";
            });
            string formattedQuery = string.Join(" OR ", terms);
            
            await using var connection = new SqliteConnection(_dbConn);
            var allowFolders = new List<string>();
            foreach (var folderItem in FolderSelectorViewModel.SelectedItems)
            {
                if (folderItem.Path is not null)
                    allowFolders.Add(folderItem.Path);
            }

            var results = await connection.QueryAsync<GraphDescription>(
                @"SELECT FileName, FolderPath, highlight(GraphIndex, 2, '<b>', '</b>') AS Content, LastModified 
                        FROM GraphIndex 
                        WHERE Content MATCH @query 
                        AND FolderPath IN @folders 
                        ORDER BY bm25(GraphIndex) ASC 
                        LIMIT 1000", new { query = formattedQuery, folders = allowFolders });

            SearchedEntries.StartSuppressNotification();
            SearchedEntries.Clear();
            foreach (var result in results)
            {
                var audioFile = Path.ChangeExtension(result.FileName, Constants.AudioExtension);
                result.CanPreview = File.Exists(audioFile);
                SearchedEntries.Add(result);
            }
            SearchedEntries.StopSuppressNotification();
        }
        catch (Exception e)
        {
            SearchedEntries.Clear();

            _toastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("Search Failed!")
                .WithContent($"{e.Message}!")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Dismiss().ByClicking()
                .Queue();
        }
        finally
        {
            IsBusy = false;   
        }
    }
    
    [RelayCommand]
    public void PlaySound(GraphDescription item)
    {
        var filePath = Path.ChangeExtension(item.FileName, Constants.AudioExtension);
        if(!File.Exists(filePath))
        {
            _toastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("File not found!")
                .WithContent($"{filePath} doesn't exist!")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Dismiss().ByClicking()
                .Queue();
            return;
        }
        
        _audioService.Play(filePath);
    }
    
    [RelayCommand]
    public async Task CopyData(string fileName)
    {
        var graphFile = Path.ChangeExtension(fileName, Constants.GraphExtension);
        if (!File.Exists(graphFile))
        {
            _toastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("Error!")
                .WithContent("Not found the graph file of the selected item!")
                .Dismiss().After(TimeSpan.FromSeconds(1))
                .Queue();
            return;
        }

        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    var text = await File.ReadAllTextAsync(graphFile);
                    await clipboard.SetTextAsync(text);
                    return;
                }
            }
        }
        catch (Exception e)
        {
            _toastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("Error!")
                .WithContent($"{e.Message}")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Dismiss().ByClicking()
                .Queue();
            return;
        }
        
        _toastManager.CreateToast()
            .OfType(NotificationType.Error)
            .WithTitle("Error!")
            .WithContent("Unable to access the clipboard!")
            .Dismiss().After(TimeSpan.FromSeconds(2))
            .Queue();
    }
}