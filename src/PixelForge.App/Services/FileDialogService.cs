using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace PixelForge.App.Services;

/// <summary>
/// Implements native desktop file dialogs using Avalonia's IStorageProvider.
/// </summary>
public class FileDialogService : IFileDialogService
{
    private static readonly FilePickerFileType PxProjFileType = new("PixelForge Project (*.pxproj)")
    {
        Patterns = new[] { "*.pxproj" }
    };

    private static readonly FilePickerFileType ImageFilesType = new("Image Files (*.png, *.jpg, *.jpeg, *.bmp, *.webp)")
    {
        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp" }
    };

    private static readonly FilePickerFileType PngFileType = new("PNG Image (*.png)")
    {
        Patterns = new[] { "*.png" }
    };

    private static readonly FilePickerFileType BmpFileType = new("Lossless BMP Image (*.bmp)")
    {
        Patterns = new[] { "*.bmp" }
    };

    private static IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow?.StorageProvider;
        }

        return null;
    }

    public async Task<string?> ShowOpenProjectDialogAsync()
    {
        var storage = GetStorageProvider();
        if (storage == null)
        {
            return null;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open PixelForge Project",
            AllowMultiple = false,
            FileTypeFilter = new[] { PxProjFileType, FilePickerFileTypes.All }
        }).ConfigureAwait(false);

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task<string?> ShowOpenImageDialogAsync()
    {
        var storage = GetStorageProvider();
        if (storage == null)
        {
            return null;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Reference Image",
            AllowMultiple = false,
            FileTypeFilter = new[] { ImageFilesType, FilePickerFileTypes.ImageAll, FilePickerFileTypes.All }
        }).ConfigureAwait(false);

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task<string?> ShowSaveProjectDialogAsync(string? defaultFileName = null)
    {
        var storage = GetStorageProvider();
        if (storage == null)
        {
            return null;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Project",
            DefaultExtension = "pxproj",
            SuggestedFileName = defaultFileName ?? "untitled.pxproj",
            FileTypeChoices = new[] { PxProjFileType, FilePickerFileTypes.All }
        }).ConfigureAwait(false);

        return file?.Path.LocalPath;
    }

    public async Task<string?> ShowExportImageDialogAsync(string defaultExtension = "png", string? defaultFileName = null)
    {
        var storage = GetStorageProvider();
        if (storage == null)
        {
            return null;
        }

        bool isBmp = string.Equals(defaultExtension, "bmp", StringComparison.OrdinalIgnoreCase);

        var choices = isBmp
            ? new[] { BmpFileType, PngFileType, FilePickerFileTypes.All }
            : new[] { PngFileType, BmpFileType, FilePickerFileTypes.All };

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Canvas Image",
            DefaultExtension = isBmp ? "bmp" : "png",
            SuggestedFileName = defaultFileName ?? (isBmp ? "export.bmp" : "export.png"),
            FileTypeChoices = choices
        }).ConfigureAwait(false);

        return file?.Path.LocalPath;
    }
}
