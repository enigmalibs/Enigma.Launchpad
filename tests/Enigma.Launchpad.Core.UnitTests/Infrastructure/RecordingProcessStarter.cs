using System;
using System.Collections.Generic;
using System.Diagnostics;
using Enigma.Launchpad.Core.Launching;

namespace Enigma.Launchpad.Core.UnitTests.Infrastructure;

/// <summary>
/// Records what would have been started, and optionally fails the way the operating system can.
/// </summary>
internal sealed class RecordingProcessStarter : IProcessStarter
{
    public List<ProcessStartInfo> Started { get; } = [];

    public Exception? Failure { get; set; }

    public void Start(ProcessStartInfo startInfo)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Started.Add(startInfo);
    }
}
