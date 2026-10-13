namespace Enigma.Launchpad.Core.Launching;

/// <summary>
/// How an attempt to open a shortcut ended.
/// </summary>
public enum LaunchOutcome
{
    /// <summary>The target was handed to the operating system.</summary>
    Launched,

    /// <summary>The file or directory the shortcut points at no longer exists.</summary>
    TargetMissing,

    /// <summary>The shortcut's target is not one this application will open (an unsupported address).</summary>
    Refused,

    /// <summary>The operating system could not start it.</summary>
    Failed,
}

/// <summary>
/// The outcome of opening a shortcut, with a message worded for the user when it did not open.
/// </summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="Message">Why it did not open; <see langword="null"/> when it did.</param>
public sealed record LaunchResult(LaunchOutcome Outcome, string? Message = null)
{
    /// <summary>
    /// Gets the result of a successful launch.
    /// </summary>
    public static LaunchResult Launched { get; } = new(LaunchOutcome.Launched);

    /// <summary>
    /// Gets a value indicating whether the target was handed to the operating system.
    /// </summary>
    public bool Succeeded => Outcome == LaunchOutcome.Launched;
}
