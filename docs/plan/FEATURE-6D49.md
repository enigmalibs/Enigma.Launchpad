# FEATURE-6D49 — Release 1.2.0

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-6d49-release-1-2-0`
**Run:** feature/2026-10-15-release-1-2-0

## Objective

Cut version **1.2.0**. The release covers the work integrated on `develop` since the `1.1.0` tag:

- **FEATURE-0BB1** — group columns: a group takes the full line, two thirds or one third of it, groups that fit share
  a line, and a group can be dragged beside another one.

The run makes the in-repo edits. The merges, the tag, the pushes and the publish are printed for the user to run.

## Context & constraints

- **An app, not a package** (`dotnet-release`, *App vs library*). No project has a `PackageId`, so there is no
  `dotnet pack`, no NuGet push and no package metadata. The version bump, the release notes, the README callout and
  the tag apply.
- `Directory.Build.props` holds `<Version>1.1.0</Version>` once for the whole solution. Core and Desktop ship as one
  application.
- The `1.1.0` tag sits on `main`'s merge of `develop` (0187b66), whose content equals `develop` at 6654a01. The
  repository's tags are bare `X.Y.Z`.
- `docs/RELEASE.md` exists and is generic in `X.Y.Z`: it is followed, not rewritten.
- The house precedent is this repository's own 1.1.0 section of `RELEASENOTES.md`:
  - a dated `## X.Y.Z — <date>` section, then a short summary list;
  - themed sections, *Changes*, *Upgrading from X.Y*, *Dependencies*, *Version*.
- `dotnet list package --outdated` on 2026-10-15: only Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent,
  Avalonia.Fonts.Inter, Avalonia.Headless and Avalonia.Skia (12.1.1 → 12.1.4). This set is coupled with
  Enigma.Avalonia.Desktop 1.2.0, still its latest.
- No package was added since 1.1.0, so the runtime licence audit of FEATURE-10CE still holds.
- On data:
  - 1.2.0 still writes `launchpad.json` at format version 3. Each group gains a `width` member (`full`, `twoThirds`
    or `oneThird`); a group without one is full width;
  - exports stay at format version 2 and carry a group's `width` only when it is not full;
  - 1.1.0's readers skip members they do not know (`System.Text.Json`'s default; neither serializer sets
    `UnmappedMemberHandling`). So 1.1.0 opens a 1.2.0 file and imports a 1.2.0 export with every group at full width,
    and drops the widths at its next save.

## Steps

1. `Directory.Build.props` — `<Version>1.2.0</Version>`.
2. `Directory.Packages.props` — unchanged: nothing outside the Avalonia set is outdated, and the set is held at 12.1.1.
3. `RELEASENOTES.md` — prepend `## 1.2.0 — 2026-10-15`, in 1.1.0's shape:
   - a summary list;
   - *Group columns* (widths, the Width menu, lines of three columns, the narrow-window fallback);
   - *Drag and drop* (a group dropped beside another, the gap as wide as the group, the copy following both ways);
   - *Changes* (*Move earlier* / *Move later*, narrow cards' names trimmed);
   - *Upgrading from 1.1* (the file format unchanged, exports carrying widths, going back to 1.1);
   - *Dependencies* (none moved, the Avalonia set held back);
   - *Version*.
4. `README.md` — the callout becomes *What's new in 1.2*.
5. `SECURITY.md` — 1.2.x supported; 1.1.x and 1.0.x not.
6. Pre-flight in Release and in Debug, then the version read off the build with the runbook's Linux recipe.
7. Print the runbook:
   - merge into `develop`, then `main`;
   - tag `1.2.0`;
   - push;
   - publish and install.

## Acceptance criteria

- `dotnet build Enigma.Launchpad.slnx -c Release` is clean, with zero warnings, and
  `dotnet test --solution Enigma.Launchpad.slnx -c Release` is green. The same holds in Debug.
- The built assemblies carry `1.2.0.0` and the informational version `1.2.0+<commit>`.
- `RELEASENOTES.md`'s top section is `1.2.0 — 2026-10-15` and covers group widths and columns, dragging a group
  beside another, the renamed moves, the upgrade and the way back, and the dependencies. The README callout names
  1.2. `SECURITY.md` supports 1.2.x.
- Nothing in the dev runs `git tag`, `git push`, a merge into `develop` or `main`, `dotnet pack` or
  `dotnet nuget push`.

## Out of scope

- NuGet packaging.
- An MSI profile.
- Bumping the Avalonia set.
- Showing the version inside the application.
- A new licence audit.
- Tagging, merging into `develop` or `main`, pushing.

## Decisions taken autonomously

| Question             | Chosen                                                                     | Why                                                                                                                           | Alternatives rejected                       |
|----------------------|----------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------|
| The version number   | 1.2.0, as asked                                                            | The user named it; an additive feature with the file format unchanged is a minor release under Semantic Versioning            | 1.1.1                                       |
| Phasing              | One phase                                                                  | A routine release (`dotnet-release`): a version, notes, a callout and a runbook                                               | Separate notes and version phases           |
| Release-notes shape  | 1.1.0's                                                                    | The repository's own precedent for a later release                                                                            | The template's *New Features · Fixes*        |
| Dependency refresh   | None; the Avalonia set held at 12.1.1                                      | Nothing else is outdated; the set moves with Enigma.Avalonia.Desktop, whose 1.2.0 is built against 12.1.1 and is still latest | Bumping Avalonia to 12.1.4                  |
| `SECURITY.md`        | 1.2.x supported; 1.1.x and 1.0.x not                                       | The policy covers the latest release, as at 1.1.0                                                                             | Supporting 1.1.x too                        |
| Licence audit        | Not repeated                                                               | First release only (`dotnet-release`), and no package was added since                                                         | Re-auditing every package                   |
| MSI profile          | None                                                                       | Declined at 1.0.0 (`docs/RELEASE.md`); the release's packaging is the Linux installer                                         | Generating the first profile                |
| Tag                  | Bare `1.2.0`, printed                                                      | Matches `1.0.0` and `1.1.0`; a run never tags                                                                                 | `v1.2.0`; tagging in the run                |
| Going back to 1.1    | Documented in *Upgrading from 1.1*: the file opens, the widths are lost    | 1.1.0 skips the unknown `width` member and writes groups without it; the user should know before trying 1.2                   | Leaving it out; a backup copy of the file   |
