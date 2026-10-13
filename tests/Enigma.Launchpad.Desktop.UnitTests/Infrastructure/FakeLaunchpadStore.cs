using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Core.Shortcuts;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// An in-memory store that records every save, and can fail or hold a save the way a disk can.
/// </summary>
internal sealed class FakeLaunchpadStore : ILaunchpadStore
{
    private readonly object _sync = new();
    private readonly List<LaunchpadData> _saved = [];

    public FakeLaunchpadStore(LaunchpadData? data = null) => Data = data ?? LaunchpadData.Empty;

    public LaunchpadData Data { get; set; }

    public string? UnreadableFileMovedTo { get; set; }

    public Exception? LoadFailure { get; set; }

    public Exception? SaveFailure { get; set; }

    /// <summary>
    /// When set, every save waits for this task before it completes.
    /// </summary>
    public Task? SaveGate { get; set; }

    public int LoadCount { get; private set; }

    public string FilePath => "launchpad.json";

    public IReadOnlyList<LaunchpadData> Saved
    {
        get
        {
            lock (_sync)
            {
                return [.. _saved];
            }
        }
    }

    public LaunchpadData? LastSaved
    {
        get
        {
            lock (_sync)
            {
                return _saved.Count == 0 ? null : _saved[^1];
            }
        }
    }

    public Task<LaunchpadLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        LoadCount++;

        return LoadFailure is not null
            ? Task.FromException<LaunchpadLoadResult>(LoadFailure)
            : Task.FromResult(new LaunchpadLoadResult(Data, UnreadableFileMovedTo));
    }

    public async Task SaveAsync(LaunchpadData data, CancellationToken cancellationToken = default)
    {
        if (SaveGate is { } gate)
        {
            await gate.ConfigureAwait(false);
        }

        if (SaveFailure is not null)
        {
            throw SaveFailure;
        }

        lock (_sync)
        {
            _saved.Add(data);
        }

        Data = data;
    }
}
