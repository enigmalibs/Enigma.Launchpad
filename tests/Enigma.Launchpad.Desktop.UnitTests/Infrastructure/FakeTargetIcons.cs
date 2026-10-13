using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// Answers program or website icon requests from a script, recording each one; can hold the answers until the
/// test releases them.
/// </summary>
internal sealed class FakeTargetIcons : IProgramIcons, IWebsiteIcons
{
    private readonly List<(string Path, TaskCompletionSource<byte[]?> Answer)> _held = [];

    public Func<string, byte[]?> Answer { get; set; } = _ => null;

    /// <summary>
    /// When set, requests stay unanswered until <see cref="Release"/>.
    /// </summary>
    public bool Hold { get; set; }

    public List<string> Requests { get; } = [];

    /// <summary>
    /// The paths whose icon was asked for again, in order; they are in <see cref="Requests"/> too.
    /// </summary>
    public List<string> Refreshes { get; } = [];

    public Task<byte[]?> RefreshAsync(string path, CancellationToken cancellationToken = default)
    {
        Refreshes.Add(path);
        return GetAsync(path, cancellationToken);
    }

    public Task<byte[]?> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        Requests.Add(path);

        if (!Hold)
        {
            return Task.FromResult(Answer(path));
        }

        TaskCompletionSource<byte[]?> answer = new();
        _held.Add((path, answer));
        return answer.Task;
    }

    /// <summary>
    /// Answers every held request for a path.
    /// </summary>
    public void Release(string path, byte[]? icon)
    {
        foreach ((string heldPath, TaskCompletionSource<byte[]?> answer) in _held)
        {
            if (heldPath == path)
            {
                answer.TrySetResult(icon);
            }
        }
    }
}

/// <summary>
/// An image that draws nothing, for ViewModel tests with no Avalonia platform.
/// </summary>
internal sealed class StubImage : IImage
{
    public Size Size => new(48, 48);

    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
    }
}
