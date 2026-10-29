using System;
using System.IO;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// The repository the tests run from, for the files that ship beside the code: the packaging scripts and their icons.
/// </summary>
internal static class RepositoryRoot
{
    /// <summary>
    /// Finds the first directory above the test's output that holds the solution.
    /// </summary>
    /// <returns>The repository's root.</returns>
    public static string Find()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Enigma.Launchpad.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The repository root was not found above " + AppContext.BaseDirectory);
    }
}
