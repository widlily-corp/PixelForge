using PixelForge.Core.Models;

namespace PixelForge.Core.Services;

/// <summary>
/// Provides project persistence and validation operations for .pxproj files.
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// Saves the project to a .pxproj JSON file.
    /// </summary>
    Task SaveProjectAsync(Project project, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads and validates a project from a .pxproj JSON file.
    /// </summary>
    Task<Project> LoadProjectAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates project data integrity and structure.
    /// </summary>
    void ValidateProject(Project project);
}
