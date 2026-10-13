using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// Records what would have been opened, revealed or copied.
/// </summary>
internal sealed class RecordingShortcutActions : IShortcutActions
{
    public List<(string Action, ShortcutKind Kind, Shortcut Shortcut)> Performed { get; } = [];

    public bool Open(ShortcutKind kind, Shortcut shortcut)
    {
        Performed.Add(("Open", kind, shortcut));
        return true;
    }

    public bool Reveal(ShortcutKind kind, Shortcut shortcut)
    {
        Performed.Add(("Reveal", kind, shortcut));
        return true;
    }

    public bool OpenInTerminal(Shortcut shortcut)
    {
        Performed.Add(("Terminal", ShortcutKind.Directory, shortcut));
        return true;
    }

    public Task CopyTargetAsync(Shortcut shortcut)
    {
        Performed.Add(("Copy", default, shortcut));
        return Task.CompletedTask;
    }

    public List<string> OpenedFolders { get; } = [];

    public bool OpenFolder(string path)
    {
        OpenedFolders.Add(path);
        return true;
    }
}
