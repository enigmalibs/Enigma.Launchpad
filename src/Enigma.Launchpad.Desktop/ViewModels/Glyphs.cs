using System;
using System.Collections.Generic;
using System.Text;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;

namespace Enigma.Launchpad.Desktop.ViewModels;

/// <summary>
/// The glyphs the launchpad draws for what it holds: a kind of shortcut, a workspace.
/// </summary>
public static class Glyphs
{
    /// <summary>
    /// The glyph of a workspace that names none, or one this build does not know.
    /// </summary>
    public const PhosphorIcon DefaultWorkspace = PhosphorIcon.SquaresFour;

    /// <summary>
    /// The glyph a new workspace starts with.
    /// </summary>
    public const PhosphorIcon NewWorkspace = PhosphorIcon.Briefcase;

    /// <summary>
    /// Gets the glyphs a workspace can be given in its form, in the order the form shows them.
    /// </summary>
    public static IReadOnlyList<PhosphorIcon> WorkspaceChoices { get; } =
    [
        PhosphorIcon.SquaresFour, PhosphorIcon.Briefcase, PhosphorIcon.Code, PhosphorIcon.TerminalWindow,
        PhosphorIcon.GitBranch, PhosphorIcon.Bug, PhosphorIcon.Database, PhosphorIcon.Cloud,
        PhosphorIcon.Cpu, PhosphorIcon.Desktop, PhosphorIcon.Robot, PhosphorIcon.Rocket,
        PhosphorIcon.Flask, PhosphorIcon.Atom, PhosphorIcon.Wrench, PhosphorIcon.Gear,
        PhosphorIcon.Folder, PhosphorIcon.Package, PhosphorIcon.BookOpen, PhosphorIcon.Notebook,
        PhosphorIcon.Article, PhosphorIcon.Newspaper, PhosphorIcon.GraduationCap, PhosphorIcon.ChartBar,
        PhosphorIcon.Calendar, PhosphorIcon.Envelope, PhosphorIcon.ChatCircle, PhosphorIcon.Users,
        PhosphorIcon.House, PhosphorIcon.Coffee, PhosphorIcon.GameController, PhosphorIcon.MusicNotes,
        PhosphorIcon.Headphones, PhosphorIcon.FilmSlate, PhosphorIcon.Camera, PhosphorIcon.PaintBrush,
        PhosphorIcon.Palette, PhosphorIcon.Heart, PhosphorIcon.Star, PhosphorIcon.Lightning,
        PhosphorIcon.Leaf, PhosphorIcon.Airplane, PhosphorIcon.ShoppingCart, PhosphorIcon.Globe,
    ];

    /// <summary>
    /// Gets the glyph of a kind of shortcut, which its groups and its tiles without an icon of their own show.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The glyph.</returns>
    public static PhosphorIcon OfKind(ShortcutKind kind)
        => kind switch
        {
            ShortcutKind.Application => PhosphorIcon.AppWindow,
            ShortcutKind.Directory => PhosphorIcon.Folder,
            ShortcutKind.Document => PhosphorIcon.FileText,
            _ => PhosphorIcon.Globe,
        };

    /// <summary>
    /// Gets the glyph a workspace names.
    /// </summary>
    /// <param name="name">The glyph's name, as the workspace stores it.</param>
    /// <returns>The glyph; <see cref="DefaultWorkspace"/> for no name, or a name that is not a glyph's.</returns>
    public static PhosphorIcon OfWorkspace(string? name)
        => (name is { Length: > 0 } && char.IsAsciiLetter(name[0]) && Enum.TryParse(name, ignoreCase: false, out PhosphorIcon glyph) && Enum.IsDefined(glyph))
            ? glyph
            : DefaultWorkspace;

    /// <summary>
    /// Words a glyph's name for people: <c>GameController</c> reads "Game controller".
    /// </summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>Its name, in words.</returns>
    public static string Describe(PhosphorIcon glyph)
    {
        string name = glyph.ToString();
        StringBuilder words = new(name.Length + 4);

        for (int index = 0; index < name.Length; index++)
        {
            char character = name[index];

            if ((index > 0) && char.IsAsciiLetterUpper(character))
            {
                words.Append(' ').Append(char.ToLowerInvariant(character));
            }
            else
            {
                words.Append(character);
            }
        }

        return words.ToString();
    }
}
