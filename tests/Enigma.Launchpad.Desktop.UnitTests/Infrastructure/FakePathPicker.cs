using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// A picker that answers from a script and records where it was asked to start.
/// </summary>
internal sealed class FakePathPicker : IPathPicker
{
    public string? NextFile { get; set; }

    public string? NextFolder { get; set; }

    public string? NextImageFile { get; set; }

    public string? NextJsonFileToOpen { get; set; }

    public string? NextJsonFileToSave { get; set; }

    public List<(string Kind, string? StartPath)> Requests { get; } = [];

    public List<(string Title, string? SuggestedFileName)> JsonRequests { get; } = [];

    public Task<string?> PickFileAsync(string title, string? startPath)
    {
        Requests.Add(("File", startPath));
        return Task.FromResult(NextFile);
    }

    public Task<string?> PickFolderAsync(string title, string? startPath)
    {
        Requests.Add(("Folder", startPath));
        return Task.FromResult(NextFolder);
    }

    public Task<string?> PickImageFileAsync(string title)
    {
        Requests.Add(("Image", null));
        return Task.FromResult(NextImageFile);
    }

    public Task<string?> PickJsonFileToOpenAsync(string title)
    {
        JsonRequests.Add((title, null));
        return Task.FromResult(NextJsonFileToOpen);
    }

    public Task<string?> PickJsonFileToSaveAsync(string title, string suggestedFileName)
    {
        JsonRequests.Add((title, suggestedFileName));
        return Task.FromResult(NextJsonFileToSave);
    }
}
