using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SnapRestore.Services;
using SnapRestore.Services.Abstraction;
using SnapRestore.ViewModels;
using SnapRestore.Views;

namespace SnapRestore;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        // Services
            services.AddSingleton<ISnapchatExportService, SnapchatExportService>();
            services.AddSingleton<IMemoryProcessingService, MemoryProcessingService>();
            services.AddSingleton<IOverlayService, OverlayService>();
            services.AddSingleton<IMemoriesHistoryService, MemoriesHistoryService>();
            services.AddSingleton<IExifToolService, ExifToolService>();
            services.AddSingleton<IExternalToolResolver, ExternalToolResolver>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();

        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
