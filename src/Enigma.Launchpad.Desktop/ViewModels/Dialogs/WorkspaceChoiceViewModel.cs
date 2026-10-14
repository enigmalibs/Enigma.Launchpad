using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;

namespace Enigma.Launchpad.Desktop.ViewModels.Dialogs;

/// <summary>
/// One workspace a dialog offers.
/// </summary>
/// <param name="Workspace">The workspace.</param>
public sealed record WorkspaceOption(Workspace Workspace)
{
    /// <summary>
    /// Gets the workspace's title.
    /// </summary>
    public string Title => Workspace.Title;

    /// <summary>
    /// Gets the workspace's glyph.
    /// </summary>
    public PhosphorIcon Glyph => Glyphs.OfWorkspace(Workspace.Icon);
}

/// <summary>
/// A dialog choosing one workspace from a list — where to move a group, for one.
/// </summary>
/// <remarks>Nothing is chosen at first, so the confirm button waits for the user's choice.</remarks>
public sealed class WorkspaceChoiceViewModel : ObservableObject
{
    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="message">What the dialog says above the list.</param>
    /// <param name="workspaces">The workspaces to choose from, in the order to show them.</param>
    public WorkspaceChoiceViewModel(string message, IEnumerable<Workspace> workspaces)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(workspaces);

        Message = message;
        Choices = workspaces.Select(workspace => new WorkspaceOption(workspace)).ToArray();
    }

    /// <summary>
    /// Gets what the dialog says above the list.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the workspaces to choose from.
    /// </summary>
    public IReadOnlyList<WorkspaceOption> Choices { get; }

    /// <summary>
    /// Gets or sets the chosen workspace; <see langword="null"/> until one is chosen.
    /// </summary>
    public WorkspaceOption? Selected
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a workspace is chosen.
    /// </summary>
    public bool HasSelection => Selected is not null;
}
