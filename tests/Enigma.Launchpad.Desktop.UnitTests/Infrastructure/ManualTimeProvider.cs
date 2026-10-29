using System;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// A clock that moves only when a test moves it, timestamps included, so elapsed time can be asserted.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow() => _now;

    public override long GetTimestamp() => _now.UtcTicks;

    public void Advance(TimeSpan by) => _now += by;
}
