using System;

namespace Enigma.Launchpad.Core.UnitTests.Infrastructure;

/// <summary>
/// A clock that always says the same time.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
