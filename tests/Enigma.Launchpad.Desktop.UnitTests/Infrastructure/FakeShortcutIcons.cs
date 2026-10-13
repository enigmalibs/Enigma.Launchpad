using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// The tiles' icon service, for ViewModel tests with no Avalonia platform: finds icons from a script — or holds the
/// answers until the test releases them — and shows a kept icon as a <see cref="StubImage"/>.
/// </summary>
internal sealed class FakeShortcutIcons : IShortcutIcons
{
    private readonly List<(string Target, TaskCompletionSource<IconImage?> Answer)> _held = [];

    /// <summary>
    /// The image found for a target; <see langword="null"/> for none.
    /// </summary>
    public Func<string, IImage?> Answer { get; set; } = _ => null;

    /// <summary>
    /// When set, finds stay unanswered until <see cref="Release"/>.
    /// </summary>
    public bool Hold { get; set; }

    /// <summary>
    /// The targets whose icon was looked for, in order.
    /// </summary>
    public List<string> Requests { get; } = [];

    /// <summary>
    /// The targets whose icon was looked for again, in order; they are in <see cref="Requests"/> too.
    /// </summary>
    public List<string> Refreshes { get; } = [];

    /// <summary>
    /// The kept icons that were shown, in order.
    /// </summary>
    public List<ShortcutIcon> Shown { get; } = [];

    /// <summary>
    /// What showing a kept icon gives, by its image text; a fresh <see cref="StubImage"/> for any other.
    /// </summary>
    public Dictionary<string, IImage?> Images { get; } = [];

    /// <summary>
    /// The image files read as icons, in order.
    /// </summary>
    public List<string> ReadFiles { get; } = [];

    /// <summary>
    /// What reading a file throws; <see langword="null"/> to read it as <see cref="ChosenIcon"/>.
    /// </summary>
    public Exception? ReadFailure { get; set; }

    /// <summary>
    /// The icon an image file is read as: one per path, so a test can tell them apart.
    /// </summary>
    public static ShortcutIcon ChosenIcon(string path) => new($"file:{path}", IsCustom: true);

    /// <summary>
    /// The icon a found image is kept as: one per target, so a test can tell them apart.
    /// </summary>
    public static ShortcutIcon FoundIcon(string target) => new($"found:{target}");

    public bool CanFind(ShortcutKind kind) => kind is ShortcutKind.Application or ShortcutKind.Url;

    public IImage? Show(ShortcutIcon icon)
    {
        Shown.Add(icon);
        return Images.TryGetValue(icon.Png, out IImage? image) ? image : new StubImage();
    }

    public Task<IconImage?> FindAsync(ShortcutKind kind, string target, CancellationToken cancellationToken = default)
    {
        Requests.Add(target);

        if (!Hold)
        {
            return Task.FromResult(Found(target, Answer(target)));
        }

        TaskCompletionSource<IconImage?> answer = new();
        _held.Add((target, answer));
        return answer.Task;
    }

    public Task<IconImage?> RefreshAsync(ShortcutKind kind, string target, CancellationToken cancellationToken = default)
    {
        Refreshes.Add(target);
        return FindAsync(kind, target, cancellationToken);
    }

    public Task<IconImage> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ReadFiles.Add(path);

        return ReadFailure is { } failure
            ? Task.FromException<IconImage>(failure)
            : Task.FromResult(new IconImage(new StubImage(), ChosenIcon(path)));
    }

    /// <summary>
    /// Answers every held find for a target.
    /// </summary>
    public void Release(string target, IImage? image)
    {
        foreach ((string heldTarget, TaskCompletionSource<IconImage?> answer) in _held)
        {
            if (heldTarget == target)
            {
                answer.TrySetResult(Found(target, image));
            }
        }
    }

    private static IconImage? Found(string target, IImage? image) => image is null ? null : new IconImage(image, FoundIcon(target));
}
