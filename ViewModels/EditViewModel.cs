using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ProceduralSFXCompanion.Controls;
using ProceduralSFXCompanion.Models;
using ProceduralSFXCompanion.Services;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using Microsoft.Data.Sqlite;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using ProceduralSFXCompanion.Collections;
using ProceduralSFXCompanion.Utilities;

namespace ProceduralSFXCompanion.ViewModels;

public partial class EditViewModel : ViewModelBase
{
    public enum AudioCopyMode
    {
        [Description("Don't copy.")]
        Ignore,
        [Description("Copy the newest file and change its name to match the current editing files.")]
        ByDate,
        [Description("Copy the file matching the name of the current editing files.")]
        ByName
    }
    
    public FolderEditSelectorViewModel FolderEditSelectorViewModel { get; }
    private AppSettingsService _appSettingsService;
    private readonly ISukiToastManager _toastManager;
    private  readonly AudioService _audioService;
    private bool _isGraphTextChanged;
    private bool _isDescriptionTextChanged;
    
    public bool IsBusy { get; set => SetProperty(ref field, value); }

    public string? AudioFolderPath
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                _appSettingsService.AppSettings.AudioFolderToCopyPath = value;
                _appSettingsService.MarkAsModified();
            }
        }
    }

    public string? GraphText
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                _isGraphTextChanged = true;
        }
    }

    public string? DescriptionText {
        get;
        set
        {
            if (SetProperty(ref field, value))
                _isDescriptionTextChanged = true;
        }
    }
    
    public int NumFiles { get; set => SetProperty(ref field, value); }
    public bool IsAutoSave { get; set => SetProperty(ref field, value); }
    public bool CanSave { get; set => SetProperty(ref field, value); }
    public string? FileNamePrefix { get; set => SetProperty(ref field, value); }
    public string? CurrentDescriptionFileName { get; set => SetProperty(ref field, value); }
    public string? CurrentGraphFileName { get; set => SetProperty(ref field, value); }
    
    public ObservableCollection<ItemSelection> AudioCopyModes { get; set => SetProperty(ref field, value); }
    public ItemSelection? CurrentAudioCopyMode { get; set => SetProperty(ref field, value); }
    public bool IsAudioOverwrite { get; set => SetProperty(ref field, value); }
    
    public int CurrentFileIndex { get;
        set
        {
            int oldValue = field;
            if (SetProperty(ref field, value))
            {
                CanSave = CurrentFileIndex > 0;
                _ = OnCurrentFileIndexChanged(oldValue);
            }
        }
    }
    private List<FileInfo> _allFiles;
    
    public EditViewModel(AppSettingsService appSettingsService, ISukiToastManager toastManager, AudioService audioService)
    {
        _allFiles = new List<FileInfo>();
        _appSettingsService = appSettingsService;
        _toastManager = toastManager;
        _audioService = audioService;
        AudioFolderPath = appSettingsService.AppSettings.AudioFolderToCopyPath;
        FolderEditSelectorViewModel = new FolderEditSelectorViewModel(appSettingsService, appSettingsService.AppSettings.EditingFolderPaths, toastManager);
        _ = UpdateFolderChanged(FolderEditSelectorViewModel.SelectedItem?.Path);
        FolderEditSelectorViewModel.OnSelectedFolderChanged += (sender, item) => { _ = UpdateFolderChanged(item?.Path); };
        IsAutoSave = true;

        AudioCopyModes = new ObservableCollection<ItemSelection>();
        foreach (var item in Enum.GetValues(typeof(AudioCopyMode)).Cast<AudioCopyMode>())
        {
            var newMode = new ItemSelection
            {
                Index = (int)item,
                Name = StringUtils.SplitCamelCase(item.ToString()),
                Description = EnumUtils.GetEnumDescription(item)
            };
            AudioCopyModes.Add(newMode);
        }
    }
    
    private async Task UpdateFolderChanged(string? folderPath)
    {
        if(IsBusy)
            return;
        
        if (folderPath is null)
            return;

        if (!Directory.Exists(folderPath))
        {
            _toastManager.CreateToast().OfType(NotificationType.Error)
                                        .WithTitle("Folder not found!")
                                        .WithContent($"{folderPath} does not exist!")
                                        .Dismiss().After(TimeSpan.FromSeconds(3))
                                        .Dismiss().ByClicking().Queue();
            return;
        }
        
        try
        {
            IsBusy = true;
            await SaveFileIfNeededAsync(CurrentFileIndex);
            
            NumFiles = 0;
            CurrentFileIndex = 0;
            
            await Task.Run(() =>
            {
                _allFiles = FileUtilities.GetFilesByCreationDate(folderPath);
                for (int i = 0; i < _allFiles.Count;)
                {
                    var currentFile = _allFiles[i];
                    var msFile = Path.ChangeExtension(currentFile.FullName, Constants.GraphExtension);
                    if (!File.Exists(msFile))
                    {
                        _allFiles.RemoveAt(i);
                        Dispatcher.UIThread.Post(() =>
                        {
                            _toastManager.CreateToast().OfType(NotificationType.Warning)
                                .WithTitle("Graph Not Found!")
                                .WithContent($"{currentFile.Name} is missing its graph file!")
                                .Dismiss().After(TimeSpan.FromSeconds(3)).Queue();
                        });
                        continue;
                    }

                    i++;
                }
            });
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast().OfType(NotificationType.Error)
                .WithTitle("Error!")
                .WithContent($"{ex.Message}")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Dismiss().ByClicking().Queue();
        }
        finally
        {
            IsBusy = false;
        }

        if (_allFiles.Count == 0)
            return;

        NumFiles = _allFiles.Count;
        CurrentFileIndex = 1;
    }

    private async Task OnCurrentFileIndexChanged(int oldValue)
    {
        if(IsBusy || CurrentFileIndex <= 0 || _allFiles.Count == 0)
            return;
        
        try
        {
            IsBusy = true;
            await SaveFileIfNeededAsync(oldValue); ;
            
            if (CurrentFileIndex > _allFiles.Count)
            {
                CurrentFileIndex = _allFiles.Count;
                // Return here to update the event again
                return;
            }

            var sanitizedIndex = Math.Max(0, CurrentFileIndex - 1);
            var currentFile = _allFiles[sanitizedIndex];
            var graphFilePath = Path.ChangeExtension(currentFile.FullName, Constants.GraphExtension);
            if (!File.Exists(currentFile.FullName) || !File.Exists(graphFilePath))
            {
                _toastManager.CreateToast().OfType(NotificationType.Error)
                    .WithTitle("File Not Found!")
                    .WithContent($"{currentFile.Name} is missing graph or description file!")
                    .Dismiss().After(TimeSpan.FromSeconds(2)).Queue();
                return;
            }

            GraphText = await File.ReadAllTextAsync(graphFilePath);
            DescriptionText = await File.ReadAllTextAsync(currentFile.FullName);

            //Suppress text changed on first read
            _isGraphTextChanged = false;
            _isDescriptionTextChanged = false;
        
            CurrentDescriptionFileName = Path.GetFileName(currentFile.Name);
            CurrentGraphFileName = Path.GetFileName(graphFilePath);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast().OfType(NotificationType.Error)
                .WithTitle("Error!")
                .WithContent($"{ex.Message}")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Dismiss().ByClicking().Queue();
        }
        finally
        {
            IsBusy = false;   
        }
    }
    
    [RelayCommand]
    public async Task RefreshCurrentFolder()
    {
        await UpdateFolderChanged(FolderEditSelectorViewModel.SelectedItem?.Path);
    }
    
    [RelayCommand]
    public async Task CreateNewFiles()
    {
        if(IsBusy)
            return;

        if (FolderEditSelectorViewModel.SelectedItem is null)
        {
            _toastManager.CreateToast().OfType(NotificationType.Error)
                            .WithTitle("No Selected Folder!")
                            .WithContent("Please select a folder first!")
                            .Dismiss().After(TimeSpan.FromSeconds(2))
                            .Dismiss().ByClicking().Queue();
            return;
        }
        
        var folderPath = FolderEditSelectorViewModel.SelectedItem.Path;
        if (!Directory.Exists(folderPath))
        {
            _toastManager.CreateToast().OfType(NotificationType.Error)
                        .WithTitle("Invalid Folder Path!")
                        .WithContent("The folder is either changed or removed. Please select a new folder!")
                        .Dismiss().After(TimeSpan.FromSeconds(2))
                        .Dismiss().ByClicking().Queue();
            return;
        }
        
        try
        {
            IsBusy =  true;
            
            int affix = _allFiles.Count;
            var prefix = FileNamePrefix is null ? "" : FileNamePrefix.Trim();
            string newDescriptionFilePath;
            string newGraphFilePath;
            if (String.IsNullOrEmpty(prefix))
            {
                newDescriptionFilePath = Path.Combine(folderPath, $"{affix}.txt");
                newGraphFilePath = Path.Combine(folderPath, $"{affix}{Constants.GraphExtension}");
                affix++;
            }
            else
            {
                newDescriptionFilePath = Path.Combine(folderPath, $"{prefix}.txt");
                newGraphFilePath = Path.Combine(folderPath, $"{prefix}{Constants.GraphExtension}");
            }

            while (File.Exists(newDescriptionFilePath) || File.Exists(newGraphFilePath))
            {
                newDescriptionFilePath = Path.Combine(folderPath, $"{prefix}{affix}.txt");
                newGraphFilePath = Path.Combine(folderPath, $"{prefix}{affix}{Constants.GraphExtension}");
                affix++;
            }
            
            await File.WriteAllTextAsync(newDescriptionFilePath, "");
            await File.WriteAllTextAsync(newGraphFilePath, "");
            
            FileInfo newFile = new FileInfo(newDescriptionFilePath);
            _allFiles.Add(newFile);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast().OfType(NotificationType.Error)
                .WithTitle("Error!")
                .WithContent($"{ex.Message}")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Dismiss().ByClicking().Queue();
        }
        finally
        {
            IsBusy = false;
        }
        
        NumFiles = _allFiles.Count;
        CurrentFileIndex = _allFiles.Count;
    }
    
    [RelayCommand]
    public async Task SaveCurrentFiles()
    {
        if(IsBusy || _allFiles.Count == 0)
            return;
        
        IsBusy = true;
        bool isSaveNeeded = _isDescriptionTextChanged || _isGraphTextChanged;
        if(isSaveNeeded)
            await SaveFileIndexAsync(CurrentFileIndex);
        else
        {
            var currentIndex = CurrentFileIndex - 1;
            if (currentIndex < _allFiles.Count && currentIndex >= 0)
            {
                CopyAudioIfNeeded(_allFiles[currentIndex].FullName);
            }
        }
        
        IsBusy = false;
    }

    private async Task SaveFileIfNeededAsync(int fileIndex)
    {
        if(IsAutoSave)
            await SaveFileIndexAsync(fileIndex);
    }
    
    private async Task SaveFileIndexAsync(int fileIndex)
    {
        try
        {
            bool isSaveNeeded = _allFiles.Count > 0 && (_isDescriptionTextChanged || _isGraphTextChanged);
            if(!isSaveNeeded)
                return;
            
            var currentIndex = fileIndex - 1;
            if (currentIndex >= _allFiles.Count || currentIndex < 0)
            {
                _toastManager.CreateToast().OfType(NotificationType.Error)
                    .WithTitle("Invalid File Index!")
                    .WithContent("The current index is out of range.")
                    .Dismiss().After(TimeSpan.FromSeconds(2)).Queue();
                return;
            }

            string descriptionFilePath = _allFiles[currentIndex].FullName;
            string graphFilePath = Path.ChangeExtension(_allFiles[currentIndex].FullName, Constants.GraphExtension);
            if (!File.Exists(graphFilePath) || !File.Exists(descriptionFilePath))
            {
                _toastManager.CreateToast().OfType(NotificationType.Error)
                    .WithTitle("Invalid File Paths!")
                    .WithContent("Can't find the specified graph or description file!.")
                    .Dismiss().After(TimeSpan.FromSeconds(2)).Queue();
                return;
            }
            
            if (_isDescriptionTextChanged)
                await File.WriteAllTextAsync(descriptionFilePath, DescriptionText);
            if (_isGraphTextChanged)
                await File.WriteAllTextAsync(graphFilePath, GraphText);
           
            _isDescriptionTextChanged = false;
            _isGraphTextChanged = false;

            CopyAudioIfNeeded(descriptionFilePath);

            if (isSaveNeeded)
            {
                _toastManager.CreateToast().OfType(NotificationType.Success)
                    .WithTitle("Success!.")
                    .WithContent("Files have been saved successfully.")
                    .Dismiss().After(TimeSpan.FromSeconds(.5)).Queue();
            }
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast().OfType(NotificationType.Error)
                .WithTitle("Error!")
                .WithContent($"{ex.Message}")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Dismiss().ByClicking().Queue();
        }
    }

    private void CopyAudioIfNeeded(string descriptionFilePath)
    {
        var destAudioPath = Path.ChangeExtension(descriptionFilePath, Constants.AudioExtension);
        if(File.Exists(destAudioPath) && !IsAudioOverwrite)
            return;
            
        var sourceFile = GetSelectedSourceAudioPath(destAudioPath);
        if(sourceFile is null)
            return;
        
        File.Copy(sourceFile.FullName, destAudioPath, true);
    }

    private FileInfo? GetSelectedSourceAudioPath(string destAudioPath)
    {
        if (CurrentAudioCopyMode is null 
            || String.IsNullOrWhiteSpace(AudioFolderPath) 
            || CurrentAudioCopyMode.Index == (int)AudioCopyMode.Ignore)
            return null;
        
        var directory = new DirectoryInfo(AudioFolderPath);
        if(!directory.Exists)
            return null;
        
        FileInfo? sourceFile = null;
        if (CurrentAudioCopyMode.Index == (int)AudioCopyMode.ByDate)
        {
            DateTime lastModifiedTime = new DateTime();
            foreach (var file in directory.EnumerateFiles())
            {
                if (file.CreationTime < lastModifiedTime || !String.Equals(file.Extension, Constants.AudioExtension, StringComparison.OrdinalIgnoreCase))
                    continue;
                
                lastModifiedTime = file.CreationTime;
                sourceFile = file;
            }
        }
        else
        {
            string sourceAudioPath = Path.Combine(AudioFolderPath, Path.GetFileName(destAudioPath));
            if(File.Exists(sourceAudioPath))
                sourceFile = new FileInfo(sourceAudioPath);
        }

        return sourceFile;
    }

    [RelayCommand]
    public void OnPairJump(TextBox textBox)
    {
        if(IsBusy || _allFiles.Count == 0)
            return;

        string? jumpToStr = textBox.Text;
        int newIndex;
        if (int.TryParse(jumpToStr, out int result))
        {
            newIndex = Math.Clamp(result, 1, _allFiles.Count);
        }
        else // Error when parsing -> set to the last member
        {
            newIndex = _allFiles.Count;
        }
        
        if(newIndex != CurrentFileIndex)
            CurrentFileIndex = newIndex;
        else
        {
            // Sync the string display with the real data!
            textBox.Text = $"{CurrentFileIndex}";
        }
    }
    
    [RelayCommand]
    public void ToNextPair()
    {
        if(IsBusy || _allFiles.Count == 0)
            return;
        
        CurrentFileIndex = Math.Clamp(CurrentFileIndex + 1, 1, _allFiles.Count);
    }
    
    [RelayCommand]
    public void ToPreviousPair()
    {
        if(IsBusy || _allFiles.Count == 0)
            return;
        
        CurrentFileIndex = Math.Clamp(CurrentFileIndex - 1, 1, _allFiles.Count);
    }
    
    public async Task OnAudioFolderPathClicked(object parameter)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(parameter as Control);
            if (topLevel is null)
                return;
            
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select a Folder",
                AllowMultiple = false
            });

            if (folders.Count > 0)
                AudioFolderPath = folders[0].Path.LocalPath;
        }
        catch (Exception e)
        {
            _toastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("Error!")
                .WithContent($"{e.Message}!")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Dismiss().ByClicking()
                .Queue();
        }
    }

    public async Task PlayAudio()
    {
        if(IsBusy)
            return;

        if (_allFiles.Count == 0)
        {
            _toastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("No editing files!")
                .WithContent("Please create new files (Ctrl + N) first!")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Dismiss().ByClicking()
                .Queue();
            return;
        }

        if (CurrentAudioCopyMode is null || CurrentAudioCopyMode.Index == (int)AudioCopyMode.Ignore)
        {
            _toastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("Invalid selection mode!")
                .WithContent("Please change the audio selection mode if you want to preview the audio!")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Dismiss().ByClicking()
                .Queue();
            return;
        }
        
        var currentFileIndex = CurrentFileIndex - 1;
        if(currentFileIndex < 0 || currentFileIndex >= _allFiles.Count)
            return;
        
        var destAudioPath = Path.ChangeExtension(_allFiles[currentFileIndex].FullName, Constants.AudioExtension);
        var sourceFile = GetSelectedSourceAudioPath(destAudioPath);
        if (sourceFile is null)
        {
            _toastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("No valid audio found!")
                .WithContent("Can't find an audio file matching the current selection mode!")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Dismiss().ByClicking()
                .Queue();
            return;
        }

        _audioService.Play(sourceFile.FullName);
    }
}