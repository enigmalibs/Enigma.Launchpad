using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Enigma.Avalonia.Desktop.Services;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// Lets the user pick a file or a folder, by path. A seam of its own so a form can be tested without a
/// platform picker.
/// </summary>
public interface IPathPicker
{
    /// <summary>
    /// Asks for one file.
    /// </summary>
    /// <param name="title">The picker's title.</param>
    /// <param name="startPath">A path to start from — a file or a folder; ignored when it does not exist.</param>
    /// <returns>The chosen file's path; <see langword="null"/> when the user cancelled.</returns>
    Task<string?> PickFileAsync(string title, string? startPath);

    /// <summary>
    /// Asks for one folder.
    /// </summary>
    /// <param name="title">The picker's title.</param>
    /// <param name="startPath">A folder to start from; ignored when it does not exist.</param>
    /// <returns>The chosen folder's path; <see langword="null"/> when the user cancelled.</returns>
    Task<string?> PickFolderAsync(string title, string? startPath);

    /// <summary>
    /// Asks for one JSON file to read.
    /// </summary>
    /// <param name="title">The picker's title.</param>
    /// <returns>The chosen file's path; <see langword="null"/> when the user cancelled.</returns>
    Task<string?> PickJsonFileToOpenAsync(string title);

    /// <summary>
    /// Asks where to write a JSON file; the picker confirms before an existing file is replaced.
    /// </summary>
    /// <param name="title">The picker's title.</param>
    /// <param name="suggestedFileName">The name the picker proposes.</param>
    /// <returns>The chosen path; <see langword="null"/> when the user cancelled.</returns>
    Task<string?> PickJsonFileToSaveAsync(string title, string suggestedFileName);
}

/// <summary>
/// Default <see cref="IPathPicker"/>, over the Enigma file and folder dialog services.
/// </summary>
public sealed class PathPicker : IPathPicker
{
    private static readonly FilePickerFileType JsonFiles = new("JSON files")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    private readonly IFileDialogService _files;
    private readonly IFolderDialogService _folders;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="files">The file picker service.</param>
    /// <param name="folders">The folder picker service.</param>
    public PathPicker(IFileDialogService files, IFolderDialogService folders)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(folders);

        _files = files;
        _folders = folders;
    }

    /// <inheritdoc />
    public async Task<string?> PickFileAsync(string title, string? startPath)
        => (await _files.ShowOpenFileDialogAsync(title: title, suggestedStartLocation: StartFolder(startPath))).FirstOrDefault();

    /// <inheritdoc />
    public async Task<string?> PickFolderAsync(string title, string? startPath)
        => (await _folders.ShowOpenFolderDialogAsync(title: title, suggestedStartLocation: StartFolder(startPath))).FirstOrDefault();

    /// <inheritdoc />
    public async Task<string?> PickJsonFileToOpenAsync(string title)
        => (await _files.ShowOpenFileDialogAsync(title: title, fileTypeFilter: [JsonFiles, FilePickerFileTypes.All])).FirstOrDefault();

    /// <inheritdoc />
    public Task<string?> PickJsonFileToSaveAsync(string title, string suggestedFileName)
        => _files.ShowSaveFileDialogAsync(
            title: title,
            suggestedFileName: suggestedFileName,
            defaultExtension: "json",
            showOverwritePrompt: true,
            fileTypeChoices: [JsonFiles]);

    /// <summary>
    /// The folder a picker opens in: the path itself when it is a folder, its parent when it is a file.
    /// </summary>
    private static string? StartFolder(string? path)
    {
        string trimmed = path?.Trim().Trim('"') ?? string.Empty;

        if (trimmed.Length == 0 || !Path.IsPathFullyQualified(trimmed))
        {
            return null;
        }

        return Directory.Exists(trimmed) ? trimmed : Path.GetDirectoryName(trimmed);
    }
}
