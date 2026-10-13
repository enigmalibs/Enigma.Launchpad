using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Enigma.Launchpad.Core.Launching;

namespace Enigma.Launchpad.Core.UnitTests.Infrastructure;

/// <summary>
/// Records what would have been started, and optionally fails the way the operating system can.
/// </summary>
internal sealed class RecordingProcessStarter : IProcessStarter
{
    public List<ProcessStartInfo> Started { get; } = [];

    /// <summary>
    /// Everything there was an attempt to start, whether it started or not.
    /// </summary>
    public List<ProcessStartInfo> Attempted { get; } = [];

    public Exception? Failure { get; set; }

    /// <summary>
    /// Programs that are not installed: starting one fails as the operating system fails a missing program.
    /// </summary>
    public HashSet<string> Missing { get; } = [];

    public void Start(ProcessStartInfo startInfo)
    {
        Attempted.Add(startInfo);

        if (Failure is not null)
        {
            throw Failure;
        }

        if (Missing.Contains(startInfo.FileName))
        {
            throw new Win32Exception(2, "The system cannot find the file specified.");
        }

        Started.Add(startInfo);
    }
}
