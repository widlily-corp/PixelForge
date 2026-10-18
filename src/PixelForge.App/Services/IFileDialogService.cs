namespace PixelForge.App.Services;

/// <summary>
/// Provides abstracted file dialog services for opening, saving, and exporting files.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Shows the file open dialog for .pxproj projects.
    /// </summary>
    Task<string?> ShowOpenProjectDialogAsync();

    /// <summary>
    /// Shows the file save dialog for saving .pxproj projects.
    /// </summary>
    Task<string?> ShowSaveProjectDialogAsync(string? defaultFileName = null);

    /// <summary>
    /// Shows the file save dialog for exporting images (PNG or BMP).
    /// </summary>
    Task<string?> ShowExportImageDialogAsync(string defaultExtension = "png", string? defaultFileName = null);
}
