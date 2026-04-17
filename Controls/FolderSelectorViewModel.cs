using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using ProceduralSFXCompanion.Models;
using ProceduralSFXCompanion.Services;
using ProceduralSFXCompanion.Utilities;
using ProceduralSFXCompanion.ViewModels;
using SukiUI.Toasts;

namespace ProceduralSFXCompanion.Controls;

public partial class FolderSelectorViewModel : ViewModelBase
{
    public event EventHandler<FolderItem>? OnUserAddedFolder;
    public ObservableCollection<FolderItem> FolderItems { get; set; }
    public ObservableCollection<FolderItem> SelectedItems { get; set; }
    private ISukiToastManager ToastManager { get; }
    
    private readonly AppSettingsService _appSettingsService;
    private readonly Dictionary<string, FolderItem> _savedFolderPaths;
    
    public FolderSelectorViewModel(AppSettingsService appSettingsService, 
                                   Dictionary<string, FolderItem> savedFolderPaths, 
                                   ISukiToastManager toastManager)
    {
        _appSettingsService = appSettingsService;
        _savedFolderPaths = savedFolderPaths;
        ToastManager = toastManager;
        string assetsPath = Path.Combine(AppContext.BaseDirectory, Constants.DefaultGraphFolder);
        var defaultFolderItem = new FolderItem { Path = assetsPath, IsSelected = true, IsRemovable = false };
        FolderItems = [defaultFolderItem];
        SelectedItems = [ defaultFolderItem ];
        var toDelete = new List<string>();
        foreach (var folderPath in _savedFolderPaths)
        {
            string? path = folderPath.Value.Path;
            if (!Directory.Exists(path))
            {
                toDelete.Add(folderPath.Key);
                continue;
            }
            
            FolderItems.Add(folderPath.Value);
            if (folderPath.Value.IsSelected)
                SelectedItems.Add(folderPath.Value);
        }

        if (toDelete.Count > 0)
        {
            foreach (var path in toDelete)
                _savedFolderPaths.Remove(path);
            appSettingsService.MarkAsModified();
            appSettingsService.TrySave(out var exception);
        }

        SelectedItems.CollectionChanged += OnSelectedItemsChanged;
    }

    private void OnSelectedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems?[0] is FolderItem addItem)
                {
                    addItem.IsSelected = true;
                    _appSettingsService.MarkAsModified();
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems?[0] is FolderItem removeItem)
                {
                    removeItem.IsSelected = false;
                    _appSettingsService.MarkAsModified();
                }
                break;
        }
    }

    public async void OnAddNewFolderPathClicked(object parameter)
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
                SelectedItems.Add(newItem);
                _savedFolderPaths.Add(newItem.Path, newItem);
                _appSettingsService.MarkAsModified();
                _appSettingsService.TrySave(out var exception);
                OnUserAddedFolder?.Invoke(this, newItem);
            }
        }
        catch (Exception e)
        {
            ToastManager?.CreateToast()
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
        if (!folderItem.IsRemovable)
        {
            ToastManager?.CreateToast()
                         .OfType(NotificationType.Error)
                         .WithTitle("Invalid Action!")
                         .WithContent($"Can't remove the folder {folderItem.Path}!")
                         .Dismiss().After(TimeSpan.FromSeconds(2))
                         .Dismiss().ByClicking()
                         .Queue();
            return;
        }
        
        FolderItems.Remove(folderItem);
        SelectedItems.Remove(folderItem);
        if (folderItem.Path is not null)
        {
            _savedFolderPaths.Remove(folderItem.Path);
            _appSettingsService.MarkAsModified();
            _appSettingsService.TrySave(out var exception);
        }
    }
}