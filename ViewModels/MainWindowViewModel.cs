using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.Input;
using ProceduralSFXCompanion.Models;
using ProceduralSFXCompanion.Services;
using ProceduralSFXCompanion.Utilities;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace ProceduralSFXCompanion.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string AppVersion { get;
        set => SetProperty(ref field, value);
    } = $"v{Constants.AppVersion?.ToString(3)}";
    public ISukiToastManager ToastManager { get; }
    public ISukiDialogManager DialogManager { get; }
    public AppSettingsService AppSettingsService { get; }
    public WebService WebService { get; }
    public SearchViewModel SearchViewModel { get; }
    public EditViewModel EditViewModel { get; }
    public TutorialsViewModel TutorialsViewModel { get; }
    
    public MainWindowViewModel(ISukiToastManager toastManager, ISukiDialogManager dialogManager, 
                                AppSettingsService appSettingsService, AudioService audioService, 
                                VideoService videoService, WebService webService)
    {
        ToastManager = toastManager;
        DialogManager = dialogManager;
        AppSettingsService = appSettingsService;
        WebService = webService;
        SearchViewModel = new SearchViewModel(AppSettingsService, ToastManager, audioService);
        EditViewModel = new EditViewModel(AppSettingsService, ToastManager, audioService);
        TutorialsViewModel = new TutorialsViewModel(AppSettingsService, ToastManager, videoService, webService);
    }
    
    [RelayCommand]
    private static void OpenUrl(string url) => UrlUtilities.OpenUrl(url);

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        bool hasUpdate = await CheckAppUpdate();
        if (!hasUpdate)
           hasUpdate = await CheckDefaultAssetUpdate();
        
        if (!hasUpdate)
        {
            ToastManager.CreateToast()
                .WithTitle("No update required")
                .WithContent($"You are using the latest version.")
                .Dismiss().After(TimeSpan.FromSeconds(2))
                .Dismiss().ByClicking()
                .Queue();
        }
    }

    private async Task<bool> CheckDefaultAssetUpdate()
    {
        try
        {
            var downloadLinks = await WebService.GetFromJsonAsync<DownloadLink>(Constants.LatestDefaultAsset);
            if(downloadLinks?.Url is null)
                throw new NullReferenceException("Download link is null!");
            
            var storedLink = AppSettingsService.AppSettings.DefaultAssetLink;
            if (downloadLinks.Version > storedLink.Version)
            {
                ToastManager.CreateToast()
                    .WithTitle("Default Asset has a new updated.")
                    .WithContent(
                        $"Please download then extract the files into the folder ./Resources/Default Graphs/ of the app. Once done, please choose Mark As Updated.")
                    .WithActionButton("Mark As Updated", _ =>
                    {
                        AppSettingsService.AppSettings.DefaultAssetLink = downloadLinks;
                        AppSettingsService.AppSettings.DefaultAssetLink.LastCheckTime = DateTime.UtcNow.DayOfYear;
                        AppSettingsService.MarkAsModified();
                        AppSettingsService.TrySave(out Exception? ex);
                    }, true)
                    .WithActionButton("Open Link", _ => OpenUrl(downloadLinks.Url), false)
                    .Dismiss().ByClicking()
                    .Queue();
                
                return true;
            }
        }
        catch (Exception e)
        {
            ToastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("Can't fetch default asset!")
                .WithContent($"{e.Message}")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Queue();
        }

        return false;
    }

    private async Task<bool> CheckAppUpdate()
    {
        try
        {
            using var request = WebService.RequestMessage(Constants.LatestAppVerUrl);
            request.Headers.Add("User-Agent", "ProceduralSFXCompanion");
            using var response = await WebService.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(jsonString);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                JsonElement latestRelease = doc.RootElement[0];
                string? versionStr = latestRelease.GetProperty("tag_name").GetString();
                bool isPreRelease = latestRelease.GetProperty("prerelease").GetBoolean();
                string? downloadUrl = latestRelease.GetProperty("html_url").GetString();
                
                if (versionStr is not null && downloadUrl is not null)
                {
                    string cleanVersion = Regex.Replace(versionStr, @"[^0-9.]", "");
                    Version latestVer = new Version(cleanVersion);
                    Version? currentVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                    if (currentVer is null || latestVer.CompareTo(currentVer) > 0)
                    {
                        var title = isPreRelease ? "New PreRelease" : "New Release";
                        ToastManager.CreateToast()
                            .WithTitle(title)
                            .WithContent($"{versionStr} is Now Available.")
                            .WithActionButton("Open", _ => OpenUrl(downloadUrl), true)
                            .Dismiss().ByClicking()
                            .Queue();
                        return true;
                    }
                }
                else
                {
                    ToastManager.CreateToast()
                        .OfType(NotificationType.Error)
                        .WithTitle("Unable to check for updates!")
                        .WithContent("Please try again later. If this error keeps appearing, please send a ticket to the repo!")
                        .Dismiss().After(TimeSpan.FromSeconds(2))
                        .Dismiss().ByClicking()
                        .Queue();
                }
            }
        }
        catch (Exception e)
        {
            ToastManager.CreateToast()
                .OfType(NotificationType.Error)
                .WithTitle("Error!")
                .WithContent($"{e}")
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Queue();
        }
        return false;
    }
}