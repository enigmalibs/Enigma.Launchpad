using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// Stores website icons in memory; can be made to fail its reads or its writes.
/// </summary>
internal sealed class FakeWebsiteIconStore : IWebsiteIconStore
{
    public ConcurrentDictionary<Uri, StoredWebsiteIcon> Entries { get; } = new();

    public Exception? ReadFailure { get; set; }

    public Exception? WriteFailure { get; set; }

    public Task<StoredWebsiteIcon?> ReadAsync(Uri site, CancellationToken cancellationToken = default)
        => ReadFailure is not null
            ? Task.FromException<StoredWebsiteIcon?>(ReadFailure)
            : Task.FromResult(Entries.TryGetValue(site, out StoredWebsiteIcon? entry) ? entry : null);

    public Task WriteAsync(Uri site, byte[] icon, CancellationToken cancellationToken = default)
    {
        if (WriteFailure is not null)
        {
            return Task.FromException(WriteFailure);
        }

        Entries[site] = new StoredWebsiteIcon(icon, IsFresh: true);
        return Task.CompletedTask;
    }
}
