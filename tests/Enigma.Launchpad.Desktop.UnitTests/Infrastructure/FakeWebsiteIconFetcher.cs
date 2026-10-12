using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// Answers icon downloads from a script, recording the sites asked; offline — no icon anywhere — by default.
/// </summary>
internal sealed class FakeWebsiteIconFetcher : IWebsiteIconFetcher
{
    public Func<Uri, byte[]?> Answer { get; set; } = _ => null;

    public ConcurrentQueue<Uri> Requests { get; } = new();

    public Task<byte[]?> FetchAsync(Uri site, CancellationToken cancellationToken = default)
    {
        Requests.Enqueue(site);
        return Task.FromResult(Answer(site));
    }
}
