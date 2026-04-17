using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProceduralSFXCompanion.Models;
using ProceduralSFXCompanion.Services;
using ProceduralSFXCompanion.ViewModels;
using SukiUI.Toasts;
using ProceduralSFXCompanion.Utilities;

namespace ProceduralSFXCompanion.Controls;

public partial class FolderEditSelectorViewModel : ViewModelBase
{
    public event EventHandler<FolderItem?>? OnSelectedFolderChanged;
    public ObservableCollection<FolderItem> FolderItems { get; set; }
    public FolderItem? SelectedItem
    {
        get;
        set
        {
            var oldValue = field;
            if (SetProperty(ref field, value))
            {
                OnSelectedItemsChanged(this, oldValue, value);
            }
        }
    }

    private ISukiToastManager ToastManager { get; }
    
    private readonly AppSettingsService _appSettingsService;
    private readonly Dictionary<string, FolderItem> _savedFolderPaths;
    
    public FolderEditSelectorViewModel(AppSettingsService appSettingsService, 
                                        Dictionary<string, FolderItem> savedFolderPaths, 
                                        ISukiToastManager toastManager)
    {
        _appSettingsService = appSettingsService;
        ToastManager = toastManager;
        _savedFolderPaths = savedFolderPaths;
        FolderItems = new ObservableCollection<FolderItem>();
        bool isEditingItemExist = false;
        foreach (var folderPath in _savedFolderPaths)
        {
            string? path = folderPath.Value.Path;
            if (!Directory.Exists(path))
                continue;
            
            FolderItems.Add(folderPath.Value);
            if (!isEditingItemExist && folderPath.Value.IsSelected)
            {
                isEditingItemExist = true;
                SelectedItem = folderPath.Value;
            }
        }
        
    }
    
    private void OnSelectedItemsChanged(object sender, FolderItem? oldItem, FolderItem? newItem)
    {
        oldItem?.IsSelected = false;
        newItem?.IsSelected = true;
        _appSettingsService.MarkAsModified();
        OnSelectedFolderChanged?.Invoke(sender,  newItem);
    }

    public async Task OnAddNewFolderPathClicked(object parameter)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(parameter as Control);
            if (topLevel is null)
                return;
            
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Add a Folder",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                string newFolderPath = folders[0].Path.LocalPath;
                var newItem = new FolderItem { Path = newFolderPath };
                if(_savedFolderPaths.ContainsKey(newItem.Path))
                    return;
                
                FolderItems.Add(newItem);
                SelectedItem?.IsSelected = false;
                SelectedItem = newItem;
                SelectedItem.IsSelected = true;
                _savedFolderPaths.Add(newItem.Path, newItem);
                _appSettingsService.MarkAsModified();
                _appSettingsService.TrySave(out var exception);
            }
        }
        catch (Exception e)
        {
            ToastManager.CreateToast()
                        .OfType(NotificationType.Error)
                        .WithTitle("Error!")
                        .WithContent($"{e.Message}!")
                        .Dismiss().After(TimeSpan.FromSeconds(3))
                        .Dismiss().ByClicking()
                        .Queue();
        }
    }

    [RelayCommand]
    private void RemoveFolderItem(FolderItem folderItem)
    {
        FolderItems.Remove(folderItem);
        if ((SelectedItem == folderItem || SelectedItem is null) && FolderItems.Count > 0)
        {
            SelectedItem = FolderItems[0];
            SelectedItem.IsSelected = true;
        }
        
        if (folderItem.Path is not null)
            _savedFolderPaths.Remove(folderItem.Path);
        
        _appSettingsService.MarkAsModified();
        _appSettingsService.TrySave(out var exception);
        if (exception is not null)
        {
            ToastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("Failed to save settings!")
                .WithContent($"{exception.Message}!")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Dismiss().ByClicking()
                .Queue();
        }
    }
}