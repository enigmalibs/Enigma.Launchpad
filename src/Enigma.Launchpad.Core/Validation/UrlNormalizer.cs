using System;

namespace Enigma.Launchpad.Core.Validation;

/// <summary>
/// Turns what the user typed into an http or https address, or says why it cannot be one.
/// </summary>
/// <remarks>
/// Only http and https are accepted. A URL shortcut is handed to the operating system's shell, and any
/// other scheme — <c>file:</c>, <c>javascript:</c>, a custom application protocol — would make it a way to
/// start arbitrary handlers rather than to open a web page.
/// </remarks>
public static class UrlNormalizer
{
    /// <summary>
    /// The message for an address with another scheme.
    /// </summary>
    public const string UnsupportedSchemeMessage = "Only http and https addresses are supported.";

    /// <summary>
    /// Normalises an address: trimmed, and given <c>https://</c> when it has no scheme.
    /// </summary>
    /// <param name="input">The address as typed.</param>
    /// <param name="normalized">The address to store, which keeps the user's own spelling.</param>
    /// <param name="error">Why the address is refused.</param>
    /// <returns><see langword="true"/> when the address is accepted.</returns>
    public static bool TryNormalize(string? input, out string normalized, out string? error)
    {
        normalized = string.Empty;
        string text = input?.Trim() ?? string.Empty;

        if (text.Length == 0)
        {
            error = "Enter a URL.";
            return false;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            if (HasForeignScheme(text))
            {
                error = UnsupportedSchemeMessage;
                return false;
            }

            text = "https://" + text;
        }

        if (!TryParse(text, out _, out error))
        {
            return false;
        }

        normalized = text;
        return true;
    }

    /// <summary>
    /// Parses an address that has already been normalised, for opening it.
    /// </summary>
    /// <param name="text">The stored address.</param>
    /// <param name="address">The parsed address.</param>
    /// <param name="error">Why the address is refused.</param>
    /// <returns><see langword="true"/> when the address is an absolute http or https address with a host.</returns>
    public static bool TryParse(string? text, out Uri? address, out string? error)
    {
        address = null;

        if (!Uri.TryCreate(text?.Trim(), UriKind.Absolute, out Uri? parsed))
        {
            error = "This is not a valid web address.";
            return false;
        }

        if ((parsed.Scheme != Uri.UriSchemeHttp) && (parsed.Scheme != Uri.UriSchemeHttps))
        {
            error = UnsupportedSchemeMessage;
            return false;
        }

        if (string.IsNullOrEmpty(parsed.Host))
        {
            error = "This address has no host name.";
            return false;
        }

        address = parsed;
        error = null;
        return true;
    }

    /// <summary>
    /// Whether text without <c>://</c> still starts with a scheme of its own, like <c>mailto:</c> or a
    /// drive letter. A colon followed by a digit is a port (<c>localhost:8080</c>), not a scheme.
    /// </summary>
    private static bool HasForeignScheme(string text)
    {
        int colon = text.IndexOf(':', StringComparison.Ordinal);

        if (colon <= 0)
        {
            return false;
        }

        if ((colon + 1 < text.Length) && char.IsAsciiDigit(text[colon + 1]))
        {
            return false;
        }

        if (!char.IsAsciiLetter(text[0]))
        {
            return false;
        }

        foreach (char character in text.AsSpan(1, colon - 1))
        {
            if (!char.IsAsciiLetterOrDigit(character) && (character is not ('+' or '-' or '.')))
            {
                return false;
            }
        }

        return true;
    }
}
