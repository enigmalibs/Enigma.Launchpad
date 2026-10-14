using System;
using System.Collections.Generic;
using Enigma.Launchpad.Core.Launching;

namespace Enigma.Launchpad.Core.UnitTests.Infrastructure;

/// <summary>
/// Finds the commands a test names, and nothing else, recording every lookup.
/// </summary>
internal sealed class FakeCommandResolver : ICommandResolver
{
    /// <summary>
    /// The commands there are, by name, with the program each one leads to.
    /// </summary>
    public Dictionary<string, string> Programs { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Lookups { get; } = [];

    public string? Resolve(string command)
    {
        Lookups.Add(command);
        return Programs.GetValueOrDefault(command.Trim());
    }
}
