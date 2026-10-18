using System.Text.Json;
using PixelForge.Core.Models;

namespace PixelForge.Core.Services;

/// <summary>
/// Implements saving, loading, and integrity validation for .pxproj projects.
/// </summary>
public class ProjectService : IProjectService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task SaveProjectAsync(Project project, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        ValidateProject(project);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = filePath + ".tmp";
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, project, SerializerOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* Ignore cleanup errors */ }
            }
            throw;
        }
    }

    public async Task<Project> LoadProjectAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Project file not found: {filePath}", filePath);
        }

        Project? project;
        await using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
        {
            try
            {
                project = await JsonSerializer.DeserializeAsync<Project>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Failed to parse project file '{filePath}': {ex.Message}", ex);
            }
        }

        if (project == null)
        {
            throw new InvalidDataException($"Project file '{filePath}' is empty or invalid.");
        }

        ValidateProject(project);
        return project;
    }

    public void ValidateProject(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.FormatVersion != 1)
        {
            throw new InvalidDataException($"Unsupported project format version: {project.FormatVersion}. Expected: 1.");
        }

        if (project.Width <= 0 || project.Height <= 0)
        {
            throw new InvalidDataException($"Invalid canvas dimensions: {project.Width}x{project.Height}. Must be positive integers.");
        }

        const int maxDimension = 16384;
        if (project.Width > maxDimension || project.Height > maxDimension)
        {
            throw new InvalidDataException($"Canvas dimensions exceed maximum allowed size of {maxDimension}x{maxDimension}.");
        }

        if (project.Layers == null || project.Layers.Count == 0)
        {
            throw new InvalidDataException("Project must contain at least one layer.");
        }

        int expectedByteCount = project.Width * project.Height * 4;
        for (int i = 0; i < project.Layers.Count; i++)
        {
            var layer = project.Layers[i];
            if (layer == null)
            {
                throw new InvalidDataException($"Layer at index {i} is null.");
            }

            if (layer.Pixels == null || layer.Pixels.Length != expectedByteCount)
            {
                throw new InvalidDataException(
                    $"Layer '{layer.Name}' (index {i}) has invalid pixel buffer size: {layer.Pixels?.Length ?? 0} bytes. Expected: {expectedByteCount} bytes.");
            }
        }

        if (project.ActiveLayerIndex < 0 || project.ActiveLayerIndex >= project.Layers.Count)
        {
            project.ActiveLayerIndex = Math.Clamp(project.ActiveLayerIndex, 0, project.Layers.Count - 1);
        }
    }
}
