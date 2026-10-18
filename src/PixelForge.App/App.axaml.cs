using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PixelForge.App.Services;
using PixelForge.App.ViewModels;
using PixelForge.Core.History;
using PixelForge.Core.Services;

namespace PixelForge.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new PixelForge.App.Views.MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IAutosaveService, AutosaveService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<UndoRedoService>();
        services.AddSingleton<MainViewModel>();
    }
}
