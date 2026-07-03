using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ProceduralSFXCompanion.Controls;
using ProceduralSFXCompanion.Models;
using ProceduralSFXCompanion.Services;
using SukiUI.Toasts;
using Microsoft.Data.Sqlite;
using Dapper;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProceduralSFXCompanion.Collections;
using ProceduralSFXCompanion.MetaSounds;
using ProceduralSFXCompanion.Utilities;

namespace ProceduralSFXCompanion.ViewModels;


public partial class SearchViewModel : ViewModelBase
{
    public FolderSelectorViewModel FolderSelectorViewModel { get; }
    private readonly string _dbConn;
    private readonly ISukiToastManager _toastManager;
    private readonly AudioService _audioService;
    private readonly AppSettingsService _appSettingsService;
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGraphEdited))]
    private bool _mergeOnPlayTriggers;
    partial void OnMergeOnPlayTriggersChanged(bool value)
    {
        _appSettingsService.AppSettings.SearchMergeOnPlayTriggers = value;
        _appSettingsService.MarkAsModified();
    }
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGraphEdited))]
    private bool _encloseWithComment;
    partial void OnEncloseWithCommentChanged(bool value)
    {
        _appSettingsService.AppSettings.SearchEncloseWithComment = value;
        _appSettingsService.MarkAsModified();
    }
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGraphEdited))]
    private bool _mergeSameTimes;
    partial void OnMergeSameTimesChanged(bool value)
    {
        _appSettingsService.AppSettings.SearchMergeSameTimes = value;
        _appSettingsService.MarkAsModified();
    }
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGraphEdited))]
    private bool _mergeSameFloats;
    partial void OnMergeSameFloatsChanged(bool value)
    {
        _appSettingsService.AppSettings.SearchMergeSameFloats = value;
        _appSettingsService.MarkAsModified();
    }

    public bool IsGraphEdited => MergeOnPlayTriggers || EncloseWithComment || MergeSameFloats || MergeSameTimes;

    public bool IsBusy
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public int NumRowsToSearch
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                SearchWatermark = $"Search (total entries = {value})...";
            }
        }
    }
    public string? SearchWatermark {  get; set => SetProperty(ref field, value); }

    private int _triggerLocalVariableNameCounter;
    
    public BulkObservableCollection<GraphDescription> SearchedEntries { get; set; }
    
    public SearchViewModel(AppSettingsService appSettingsService, ISukiToastManager toastManager, AudioService audioService)
    {
        SearchedEntries = new BulkObservableCollection<GraphDescription>();
        _toastManager = toastManager;
        _audioService = audioService;
        _appSettingsService = appSettingsService;
        FolderSelectorViewModel = new FolderSelectorViewModel(appSettingsService, appSettingsService.AppSettings.FolderPaths, toastManager);
        FolderSelectorViewModel.OnUserAddedFolder += (s, e) =>  { _ = OnUserAddedFolder(e); };
        FolderSelectorViewModel.SelectedItems.CollectionChanged += (_, _) =>
        {
            using var connection = new SqliteConnection(_dbConn);
            UpdateNumSearchRow(connection);
        };

        _mergeOnPlayTriggers = appSettingsService.AppSettings.SearchMergeOnPlayTriggers;
        _encloseWithComment = appSettingsService.AppSettings.SearchEncloseWithComment;
        _mergeSameFloats = appSettingsService.AppSettings.SearchMergeSameFloats;
        _mergeSameTimes = appSettingsService.AppSettings.SearchMergeSameTimes;
        
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
            // Delete if we have a folder before adding to make sure all entries are up to date
            _ = await connection.ExecuteAsync("DELETE FROM GraphIndex WHERE FolderPath = @Path",
                folderItem, transaction);
            
            StartWatching(folder);
            var files = Directory.GetFiles(folder, $"*{Constants.DescriptionExtension}", SearchOption.TopDirectoryOnly);
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
            UpdateNumSearchRow(connection);
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
                var files = Directory.GetFiles(folder, $"*{Constants.DescriptionExtension}", SearchOption.TopDirectoryOnly);
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
            
            UpdateNumSearchRow(connection);
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

    private void UpdateNumSearchRow(SqliteConnection connection)
    {
        var allowFolders = new List<string>();
        foreach (var folderItem in FolderSelectorViewModel.SelectedItems)
        {
            if (folderItem.Path is not null)
                allowFolders.Add(folderItem.Path);
        }
        NumRowsToSearch = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM GraphIndex WHERE FolderPath in @folders",
            new { folders = allowFolders });
    }

    private void DeleteEntry(string fileName)
    {
        using var connection = new SqliteConnection(_dbConn);
        connection.Open();
        connection.Execute("DELETE FROM GraphIndex WHERE FileName = @fileName", new { fileName });
    }
    
    private void StartWatching(string path)
    {
        try
        {
            if(_watchers.ContainsKey(path))
                return;
            
            var watcher = new FileSystemWatcher(path, $"*{Constants.DescriptionExtension}")
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };

            watcher.Changed += async (_, e) => await HandleUpdate(e.FullPath, path);
            watcher.Created += async (_, e) => await HandleUpdate(e.FullPath, path);
            watcher.Deleted += (_, e) => DeleteEntry(e.FullPath);
            watcher.Renamed += async (_, e) => { DeleteEntry(e.OldFullPath); 
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
        try
        {
            IsBusy = true;
            
            if (String.IsNullOrWhiteSpace(searchTerm))
                return;
        
            IEnumerable<GraphDescription>? results;
            
            if(String.Equals(searchTerm, "*"))
                results = await GetAllEntries();
            else
                results = await SearchTable(searchTerm);
            
            if(results is null)
                return;
            
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

    private async Task<IEnumerable<GraphDescription>> GetAllEntries()
    {
        await using var connection = new SqliteConnection(_dbConn);
        var allowFolders = new List<string>();
        foreach (var folderItem in FolderSelectorViewModel.SelectedItems)
        {
            if (folderItem.Path is not null)
                allowFolders.Add(folderItem.Path);
        }
                
        return  await connection.QueryAsync<GraphDescription>(
            @"SELECT FileName, FolderPath, Content, LastModified 
                        FROM GraphIndex 
                        WHERE FolderPath IN @folders", new { folders = allowFolders });
    }
    
    private async Task<IEnumerable<GraphDescription>?> SearchTable(string searchTerm)
    {
        string cleanSearch = Regex.Replace(searchTerm, @"[^a-zA-Z0-9\s""]", " ").Trim();
        if (String.IsNullOrWhiteSpace(cleanSearch))
            return null;
            
        StringBuilder inQuote = new StringBuilder();
        StringBuilder outQuote = new StringBuilder();
        const string pattern = @"""(.*?)""|([^""]+)";
        foreach (Match match in Regex.Matches(cleanSearch, pattern))
        {
            if (match.Groups[1].Success)
            {
                string text = match.Groups[1].Value.Trim();
                if (!String.IsNullOrWhiteSpace(text))
                {
                    if(inQuote.Length > 0)
                        inQuote.Append($" OR \"{text}\"");
                    else
                        inQuote.Append($"\"{text}\"");
                }
            }
            else if (match.Groups[2].Success)
            {
                string text = match.Groups[2].Value.Trim();
                if (!String.IsNullOrWhiteSpace(text))
                    outQuote.Append($" {text}");
            }
        }

        var inQuoteStr = inQuote.ToString();
        bool isHasInQuote = !String.IsNullOrEmpty(inQuoteStr);
        var outQuoteStr = outQuote.ToString();
        bool isHasOutQuote = !String.IsNullOrEmpty(outQuoteStr);
        string formattedOutQuote = "";
        if (isHasOutQuote)
        {
            var terms = outQuoteStr.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(term => $"\"{term}\"*");
            formattedOutQuote = string.Join(" OR ", terms);
        }
            
        string formattedQuery;
        if (isHasInQuote && isHasOutQuote)
            formattedQuery = $"{inQuoteStr} OR {formattedOutQuote}";
        else if (isHasInQuote)
            formattedQuery = inQuoteStr;
        else if (isHasOutQuote)
            formattedQuery = formattedOutQuote;
        else
            return null;
            
        await using var connection = new SqliteConnection(_dbConn);
        var allowFolders = new List<string>();
        foreach (var folderItem in FolderSelectorViewModel.SelectedItems)
        {
            if (folderItem.Path is not null)
                allowFolders.Add(folderItem.Path);
        }
                
        return  await connection.QueryAsync<GraphDescription>(
            @"SELECT FileName, FolderPath, highlight(GraphIndex, 2, '<b>', '</b>') AS Content, LastModified 
                        FROM GraphIndex 
                        WHERE Content MATCH @query 
                        AND FolderPath IN @folders 
                        ORDER BY bm25(GraphIndex, 0, 0, 10.0) ASC 
                        LIMIT 1000", new { query = formattedQuery, folders = allowFolders });
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
    public void OpenInExplorer(GraphDescription item)
    {
        FileUtilities.ShowInFolder(item.FileName);
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
                    
                    if (IsGraphEdited)
                    {
                        var allNodes = MsRawProcessor.ParseMetaSoundsText(text);

                        if (MergeOnPlayTriggers)
                            allNodes = MsRawProcessor.MergeOnPlayNodesToLocalTrigger(allNodes, $"Play_{_triggerLocalVariableNameCounter}");
                        
                        if(MergeSameTimes)
                            MsRawProcessor.MergeSameTimesToInputs(allNodes, _triggerLocalVariableNameCounter.ToString());
                        
                        if (MergeSameFloats)
                            MsRawProcessor.MergeSameFloatsToInputs(allNodes, _triggerLocalVariableNameCounter.ToString());

                        if (EncloseWithComment)
                        {
                            var descFile = Path.ChangeExtension(fileName, Constants.DescriptionExtension);
                            using StreamReader reader = new StreamReader(descFile);
                            string? comment = await reader.ReadLineAsync();
                            if(!String.IsNullOrWhiteSpace(comment))
                                MsRawProcessor.AddEncloseComment(allNodes, comment);
                        }
                        _triggerLocalVariableNameCounter = (_triggerLocalVariableNameCounter + 1) % 100;
                        
                        text = MsRawProcessor.SerializeToMetaSounds(allNodes);
                    }
                    
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