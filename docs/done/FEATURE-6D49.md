# FEATURE-6D49 — Release 1.2.0

**Item:** FEATURE-6D49 — Release 1.2.0
**Branch:** `feature/feature-6d49-release-1-2-0`
**Run:** feature/2026-10-15-release-1-2-0

## Summary

Version **1.2.0** is cut in the repository. The merges, the tag, the pushes and the publish are left for the user to
run. The commands are in `docs/RELEASE.md` and in the run's report.

- `Directory.Build.props`: `<Version>1.2.0</Version>` (was `1.1.0`).
  - The built assemblies carry `1.2.0.0`.
  - Their informational version is `1.2.0+<commit>`.
- `Directory.Packages.props`: unchanged. Only the Avalonia set is outdated, and it is held at 12.1.1.
- `RELEASENOTES.md` — `## 1.2.0 — 2026-10-15` prepended, in 1.1.0's shape:
  - a summary list;
  - *Group columns*: the Width submenu, lines of three columns in order, top-aligned cards, the narrow-window
    fallback, new and existing groups at full width;
  - *Drag and drop*: a group dropped beside another, the gap as wide as the group and holding still, the copy as wide
    as its card and following both ways, the unchanged rules;
  - *Changes*: *Move earlier* / *Move later*, narrow cards' names trimmed, export files carrying widths;
  - *Upgrading from 1.1*: nothing converted, `settings.json` unchanged, exports both ways, and going back to 1.1;
  - *Dependencies*, *Version*.
- `README.md` — the callout is *What's new in 1.2*.
- `SECURITY.md` — 1.2.x supported; 1.1.x and 1.0.x not.

## Files / modules touched

**Created**

- `docs/done/FEATURE-6D49.md`

**Modified**

- `Directory.Build.props` — the version
- `RELEASENOTES.md` — the 1.2.0 section
- `README.md` — the what's-new callout
- `SECURITY.md` — the supported-versions table
- `docs/roadmap.md`, `docs/plan/FEATURE-6D49.md` — statuses

## Dependency refresh

`dotnet list package --outdated`, 2026-10-15:

| Package                                                            | Before | After  | Note                                                                                             |
|--------------------------------------------------------------------|--------|--------|--------------------------------------------------------------------------------------------------|
| Avalonia, .Desktop, .Themes.Fluent, .Fonts.Inter, .Headless, .Skia | 12.1.1 | 12.1.1 | **held back**: 12.1.4 is out; the set moves with Enigma.Avalonia.Desktop 1.2.0, still its latest |

Nothing else is outdated. No package was added since 1.1.0, so FEATURE-10CE's runtime licence audit still holds and
was not repeated (`dotnet-release`: first release only).

## Deviations & follow-ups

- **Deviations:** none. The plan was followed as written.
- **Verified for the notes:** 1.1.0 opens a 1.2.0 file. Neither 1.1.0 serializer sets `UnmappedMemberHandling`
  (`git grep Unmapped 1.1.0 -- src` finds nothing), so `System.Text.Json` skips the unknown `width` member. 1.1.0's
  `ShortcutGroup` has no width, so its next save writes the groups without one.
- **Follow-ups, none blocking the release:**
  - Run the suite on Windows before announcing the release. This run is on Linux, where 7 Windows-only tests are
    skipped.
  - Enable **Private vulnerability reporting** in the GitHub repository (carried from 1.0.0), if not yet done.
  - The Avalonia set: move it to 12.1.4 with the next Enigma.Avalonia.Desktop.
  - Showing the version inside the application (carried from 1.0.0).
  - The Profiles page's card menu still says *Move up* / *Move down*. That is right for its vertical list
    (FEATURE-0BB1-PHASE02).
- **`main` and `develop`:** `main` holds the `1.1.0` tag on a merge of `develop` (0187b66) that `develop` does not
  contain. Its content equals `develop` at 6654a01, so merging `develop` into `main` is conflict-free.
- Fix budget: none used. The first build and test runs were green.
- **Documentation sweep:**
  - README: the callout, as above. Its workspace paragraph already describes the Width menu (FEATURE-0BB1-PHASE02).
  - `docs/RELEASE.md`: generic in `X.Y.Z`, and still true. Its `1.1.0` is an example of the version format, not a
    claim about the current release.
  - Nothing else names the version.
- Line endings: every touched file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx -c Release --no-incremental` — 0 warnings, 0 errors.
  `dotnet test --solution Enigma.Launchpad.slnx -c Release` — total 965, 958 passed, 0 failed, 7 skipped.
- `dotnet build Enigma.Launchpad.slnx --no-incremental` — 0 warnings, 0 errors.
  `dotnet test --solution Enigma.Launchpad.slnx` — total 965, 958 passed, 0 failed, 7 skipped.
- The built version, read with the runbook's Linux recipe:
  - `strings src/Enigma.Launchpad.Desktop/bin/Release/net10.0/Enigma.Launchpad.Core.dll | grep -m1 -o '1\.2\.0+[0-9a-f]*'`
    prints `1.2.0+273a5b67b0846e5913b4dade2d57cff54f17fe1b`, the run branch's tip at build time;
  - the file version is `1.2.0.0`.
- Nothing in this dev ran `git tag`, `git push`, a merge into `develop` or `main`, `dotnet pack` or
  `dotnet nuget push`.

## Decisions taken at build time

| Question                       | Chosen                                                                                   | Why                                                                                                       |
|--------------------------------|------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------|
| Themed sections in the notes   | *Group columns*, then *Drag and drop*                                                    | FEATURE-0BB1's two halves, in the order a user meets them: setting a width, then dragging into columns    |
| Where export widths go         | Under *Changes*, and both directions under *Upgrading from 1.1*                          | What an export holds changed; what crosses versions belongs with the upgrade                              |
| The slot rule in the notes     | Sides of a card narrower than the line; halves of a card that takes the whole line       | `DropPlacement.ColumnSlot`: a full-line card has no side, so its upper and lower halves decide            |
| Going back to 1.1              | 1.1 opens the file at full width and drops the widths at its next save                   | Verified on the 1.1.0 tag (see *Deviations & follow-ups*); no backup is needed, as nothing is converted   |
| The callout                    | The columns, the drag, and that groups keep the full line until changed                  | The one-line highlight a 1.1 user needs: nothing looks different until they set a width                   |
