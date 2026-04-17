using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProceduralSFXCompanion.Services;
using ProceduralSFXCompanion.Utilities;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace ProceduralSFXCompanion.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly HttpClient HttpClient = new HttpClient();
    
    public string AppVersion => $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
    public ISukiToastManager ToastManager { get; }
    public ISukiDialogManager DialogManager { get; }
    public AppSettingsService AppSettingsService { get; }
    public SearchViewModel SearchViewModel { get; }
    public EditViewModel EditViewModel { get; }
    
    public MainWindowViewModel(ISukiToastManager toastManager, ISukiDialogManager dialogManager, 
                                AppSettingsService appSettingsService, AudioService audioService)
    {
        ToastManager = toastManager;
        DialogManager = dialogManager;
        AppSettingsService = appSettingsService;
        SearchViewModel = new SearchViewModel(AppSettingsService, ToastManager, audioService);
        EditViewModel = new EditViewModel(AppSettingsService, ToastManager, audioService);
    }
    
    [RelayCommand]
    private static void OpenUrl(string url) => UrlUtilities.OpenUrl(url);
    
    [RelayCommand]
    private async Task CheckForUpdates()
    {
        try
        {
            string url = "https://api.github.com/repos/Hikaru-sin/ProceduralSFXCompanion/releases";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "ProceduralSFXCompanion");

            var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(jsonString);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                JsonElement latestRelease = doc.RootElement[0];
                string? versionStr = latestRelease.GetProperty("tag_name").GetString();
                bool isPreRelease = latestRelease.GetProperty("prerelease").GetBoolean();
                string? downloadUrl = latestRelease.GetProperty("html_url").GetString();
                
                if (versionStr is not null && downloadUrl is not null && AppVersion != versionStr)
                {
                    var title = isPreRelease ? "New PreRelease" : "New Release";
                    ToastManager.CreateToast()
                        .WithTitle(title)
                        .WithContent($"Information, {versionStr} is Now Available.")
                        .WithActionButton("Open", _ => OpenUrl(downloadUrl), true)
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
    }
}