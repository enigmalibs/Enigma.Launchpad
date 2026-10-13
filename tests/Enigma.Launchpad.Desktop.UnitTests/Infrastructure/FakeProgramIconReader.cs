using System;
using System.Collections.Concurrent;
using System.Threading;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// Answers icon reads from a script, recording each read and the thread it ran on; can hold reads until released.
/// </summary>
internal sealed class FakeProgramIconReader : IProgramIconReader
{
    public Func<string, ProgramIconPixels?> Answer { get; set; } = _ => null;

    /// <summary>
    /// When set, every read waits for it before answering.
    /// </summary>
    public ManualResetEventSlim? Gate { get; set; }

    /// <summary>
    /// When set, every read signals it once it has been recorded.
    /// </summary>
    public ManualResetEventSlim? Started { get; set; }

    public ConcurrentQueue<(string Path, int Size, int ThreadId)> Reads { get; } = new();

    public ProgramIconPixels? Read(string path, int size)
    {
        Reads.Enqueue((path, size, Environment.CurrentManagedThreadId));
        Started?.Set();
        Gate?.Wait(TimeSpan.FromSeconds(10));

        return Answer(path);
    }

    /// <summary>
    /// A square icon of one opaque colour.
    /// </summary>
    public static ProgramIconPixels Opaque(int size)
    {
        byte[] bgra = new byte[size * size * 4];

        for (int index = 0; index < bgra.Length; index += 4)
        {
            bgra[index] = 0x30;
            bgra[index + 1] = 0x60;
            bgra[index + 2] = 0x90;
            bgra[index + 3] = 0xFF;
        }

        return new ProgramIconPixels(size, size, bgra);
    }
}
