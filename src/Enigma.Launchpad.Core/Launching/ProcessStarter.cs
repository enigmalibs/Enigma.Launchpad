using System;
using System.Diagnostics;

namespace Enigma.Launchpad.Core.Launching;

/// <summary>
/// Starts a process. A seam of its own so a test can say "this would have been started" without
/// anything appearing on the developer's screen.
/// </summary>
public interface IProcessStarter
{
    /// <summary>
    /// Starts a process and lets go of it.
    /// </summary>
    /// <param name="startInfo">What to start.</param>
    /// <exception cref="System.ComponentModel.Win32Exception">The operating system could not start it.</exception>
    /// <exception cref="InvalidOperationException">The start information is not usable.</exception>
    void Start(ProcessStartInfo startInfo);
}

/// <summary>
/// Default <see cref="IProcessStarter"/>: <see cref="Process.Start(ProcessStartInfo)"/>.
/// </summary>
public sealed class ProcessStarter : IProcessStarter
{
    /// <inheritdoc />
    public void Start(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        // The launchpad never waits on what it starts, so the handle is released straight away.
        using Process? process = Process.Start(startInfo);
    }
}
