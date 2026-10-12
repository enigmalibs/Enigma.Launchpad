namespace Enigma.Launchpad.Core.Validation;

/// <summary>
/// The outcome of checking a shortcut's target: the form to store when it is valid, or why it is not.
/// </summary>
/// <param name="NormalizedTarget">The target as it should be stored; <see langword="null"/> when invalid.</param>
/// <param name="Error">Why the target was refused, worded for the user; <see langword="null"/> when valid.</param>
public readonly record struct TargetValidation(string? NormalizedTarget, string? Error)
{
    /// <summary>
    /// Gets a value indicating whether the target was accepted.
    /// </summary>
    public bool IsValid => Error is null;

    /// <summary>
    /// An accepted target.
    /// </summary>
    /// <param name="normalizedTarget">The target as it should be stored.</param>
    /// <returns>The outcome.</returns>
    public static TargetValidation Valid(string normalizedTarget) => new(normalizedTarget, null);

    /// <summary>
    /// A refused target.
    /// </summary>
    /// <param name="error">Why, worded for the user.</param>
    /// <returns>The outcome.</returns>
    public static TargetValidation Invalid(string error) => new(null, error);
}
