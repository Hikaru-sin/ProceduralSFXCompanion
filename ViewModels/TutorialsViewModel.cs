using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using AvaloniaEdit.Utils;
using CommunityToolkit.Mvvm.Input;
using ProceduralSFXCompanion.Controls;
using ProceduralSFXCompanion.Models;
using ProceduralSFXCompanion.Services;
using ProceduralSFXCompanion.Utilities;
using SukiUI.Toasts;

namespace ProceduralSFXCompanion.ViewModels;

public partial class TutorialsViewModel : ViewModelBase
{
    private readonly AppSettingsService _appSettingsService;
    private readonly ISukiToastManager _toastManager;
    private readonly VideoService _videoService;
    private readonly WebService _webService;
    public bool IsBusy { get; private set; } 
    public ObservableCollection<TutorialCardsModel> TutorialCards { get; set; }
    public TutorialsViewModel(AppSettingsService settingsService, ISukiToastManager toastManager, VideoService videoService, WebService webService)
    {
        _appSettingsService = settingsService;
        _toastManager = toastManager;
        _videoService = videoService;
        _webService = webService;
        TutorialCards = new ObservableCollection<TutorialCardsModel>();
        CreateTutorialCards();
    }

    private async void CreateTutorialCards()
    {
        try
        {
            IsBusy = true;

            WebTutorialsFetch? showTutorials = null;
            var currentTime = DateTime.UtcNow.DayOfYear;
            var storedTutorials = _appSettingsService.AppSettings.TutorialsFetch;
            bool bIsUpdated = false;
            if (storedTutorials?.Items is not null && storedTutorials.LastUpdated == currentTime)
                showTutorials = storedTutorials;
            else
            {
                var remoteTutorials = await FetchRemoteTutorialAsync();
                if (remoteTutorials?.Items is not null)
                {
                    showTutorials = remoteTutorials;
                    bIsUpdated = IsNewTutorialUpdated(storedTutorials, remoteTutorials);
                }
                else
                    showTutorials = storedTutorials;
            }

            if(showTutorials?.Items is null)
                return;
            
            showTutorials.LastUpdated = DateTime.UtcNow.DayOfYear;
            _appSettingsService.AppSettings.TutorialsFetch = showTutorials;
            if(bIsUpdated || storedTutorials is null || showTutorials.LastUpdated != storedTutorials.LastUpdated)
                _appSettingsService.MarkAsModified();
            
            Dictionary<string, List<WebTutorial>> tutorialDict = new Dictionary<string, List<WebTutorial>>();
            foreach (var item in showTutorials.Items)
            {
                string groupName;
                if (item.Group is null || item.Group.Count == 0)
                    groupName = Constants.UngroupTutorial; 
                else
                    groupName = item.Group[0];

                if (!tutorialDict.ContainsKey(groupName))
                    tutorialDict[groupName] = new List<WebTutorial>();
                
                tutorialDict[groupName].Add(item);
            }

            List<TutorialCardsModel> allCards = new List<TutorialCardsModel>();
            foreach (var item in tutorialDict)
            {
                allCards.Add( new TutorialCardsModel
                {
                    GroupName = item.Key,
                    Items = new ObservableCollection<WebTutorial>(item.Value.OrderBy(x => x.Order))
                });
            }

            TutorialCards = new ObservableCollection<TutorialCardsModel>(allCards.OrderBy(x => x.Items![0].Order));
            if (bIsUpdated)
            {
                _toastManager.CreateToast()
                    .OfType(NotificationType.Information)
                    .WithTitle("New tutorial(s) is available")
                    .WithContent("You can view new tutorial(s) in the tutorial tab.")
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        }
        catch (Exception e)
        {
            _toastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("Can't show tutorial!")
                .WithContent($"{e.Message}")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Queue();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<WebTutorialsFetch?> FetchRemoteTutorialAsync()
    {
        try
        {
            var remoteTutorials = await _webService.GetFromJsonAsync<WebTutorialsFetch>(Constants.WebTutorialsUrl);
            if (remoteTutorials?.Items is null)
                throw new NullReferenceException("Response is null!");
            return remoteTutorials;
        }
        catch (Exception e)
        {
            _toastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("Can't fetch web tutorial!")
                .WithContent($"{e.Message}")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Queue();
        }
        return null;
    }

    bool IsNewTutorialUpdated(WebTutorialsFetch? oldTutorials, WebTutorialsFetch? newTutorials)
    {
        if (oldTutorials?.Items is null)
            return true;

        if (newTutorials?.Items is null)
            return false;
        
        if(newTutorials.Items.Count != oldTutorials.Items.Count)
            return true;

        for (int i = 0; i < oldTutorials.Items.Count; i++)
        {
            if (!String.Equals(oldTutorials.Items[i].ContentUrl, newTutorials.Items[i].ContentUrl, StringComparison.InvariantCultureIgnoreCase))
                return true;
        }
        
        return false;
    }

    [RelayCommand]
    public void OpenTutorial(string url)
    {
        try
        {
            UrlUtilities.OpenUrl(url);
        }
        catch (Exception)
        {
            //Ignore
        }
    }
}