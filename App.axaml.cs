using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using ProceduralSFXCompanion.Views;
using ProceduralSFXCompanion.ViewModels;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.Enums;
using SukiUI.Toasts;
using ProceduralSFXCompanion.Services;

namespace ProceduralSFXCompanion;

public partial class App : Application
{
    public ServiceProvider? ServiceProvider { get; private set; }
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            services.AddSingleton(desktop);
            services.AddSingleton<MainWindowViewModel>();
            
            ServiceProvider = ConfigureServices(services);
            desktop.MainWindow = new MainWindow
            {
                DataContext = ServiceProvider.GetRequiredService<MainWindowViewModel>(),
            };
            desktop.ShutdownRequested += OnShutdownRequested;
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (ServiceProvider is not null)
        {
            var appSettings = ServiceProvider.GetRequiredService<AppSettingsService>();
            appSettings.TrySave(out Exception? ex);
            if (ex is not null)
            {
                var toastManager = ServiceProvider.GetRequiredService<ISukiToastManager>();
                toastManager.CreateToast()
                            .OfType(NotificationType.Error)
                            .WithTitle("Failed to save!")
                            .WithContent($"{ex.Message}!")
                            .Dismiss().After(TimeSpan.FromSeconds(3))
                            .Dismiss().ByClicking()
                            .Queue();
            }
        }
    }

    private static ServiceProvider ConfigureServices(ServiceCollection services)
    {
        services.AddSingleton<ISukiToastManager, SukiToastManager>();
        services.AddSingleton<ISukiDialogManager, SukiDialogManager>();
        services.AddSingleton<AppSettingsService>();
        services.AddSingleton<VideoService>();
        services.AddSingleton<AudioService>();
        services.AddSingleton<WebService>();
        
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            // Validate if forgetting to register a dependency 
            ValidateOnBuild = true 
        });
    }
}
    