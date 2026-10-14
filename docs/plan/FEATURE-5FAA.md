# FEATURE-5FAA — Release 1.1.0

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-5faa-release-1-1-0`
**Run:** bugfix/2026-10-14-release-1-1-0

## Objective

Cut version **1.1.0**. The release covers the work integrated on `develop` since the `1.0.0` tag:

- **FEATURE-1F8C** — commands found on the `PATH` as application targets;
- **FEATURE-1C0D** — workspaces, which replace the Applications, Directories and URLs pages;
- **FEATURE-1FA9** — documents;
- **FEATURE-23F9** — the drag visuals in an open workspace.

The run makes the in-repo edits. The merges, the tag, the pushes and the publish are printed for the user to run.

## Context & constraints

- **An app, not a package** (`dotnet-release`, *App vs library*). No project has a `PackageId`, so there is no
  `dotnet pack`, no NuGet push and no package metadata. The version bump, the release notes, the README callout and
  the tag apply.
- `Directory.Build.props` holds `<Version>1.0.0</Version>` once for the whole solution. Core and Desktop ship as one
  application.
- The `1.0.0` tag sits on `main`'s merge of `develop` (6499136). `main`'s content equals `develop` at 46887fd. The
  repository's tags are bare `X.Y.Z`.
- `docs/RELEASE.md` exists and is generic in `X.Y.Z`: it is followed, not rewritten.
- The house precedent for a later app release is Enigma.GitClient 6.1.0:
  - a dated `## X.Y.Z — <date>` section, then a short summary list;
  - themed sections, *Changes*, *Upgrading from X.Y*, *Dependencies*, *Version*;
  - in `SECURITY.md`, the new minor on top as supported and the older ones as not.
- `dotnet list package --outdated` on 2026-10-14:
  - Microsoft.Extensions.DependencyInjection(.Abstractions), Microsoft.Extensions.Logging.Abstractions and
    Microsoft.Extensions.Hosting: 10.0.12 → 10.0.13;
  - xunit.v3: 4.0.1 → 4.0.2;
  - Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter, Avalonia.Headless and Avalonia.Skia:
    12.1.1 → 12.1.4. This set is coupled with Enigma.Avalonia.Desktop 1.2.0, still its latest.
- No package was added since 1.0.0, so the runtime licence audit of FEATURE-10CE still holds.
- On data: 1.1.0 writes `launchpad.json` at format version 3.
  - A version 2 file (1.0.0) is copied once to `launchpad.v2.backup.json` and read into a *Shortcuts* workspace.
  - 1.0.0 refuses a version 3 file: it sets it aside as `launchpad.corrupt-<timestamp>.json` and starts empty.
- BUG-10AB comes first in this run, so the suite is green on Linux when this dev starts.

## Steps

1. `Directory.Build.props` — `<Version>1.1.0</Version>`.
2. `Directory.Packages.props`:
   - the four Microsoft.Extensions packages → 10.0.13, as a set;
   - xunit.v3 → 4.0.2;
   - the Avalonia set held at 12.1.1.
3. `RELEASENOTES.md` — prepend `## 1.1.0 — 2026-10-14`, in GitClient's shape:
   - a summary list;
   - *Workspaces*, *Documents*, *Commands as applications*, *Drag and drop*;
   - *Changes* (the three pages are gone);
   - *Upgrading from 1.0* (the automatic move into a *Shortcuts* workspace, the backup, export files, going back);
   - *Dependencies* (every `old → new`, the Avalonia set held back);
   - *Version*.
4. `README.md` — the callout becomes *What's new in 1.1*.
5. `SECURITY.md` — 1.1.x supported, 1.0.x not.
6. Pre-flight in Release and in Debug, then the version read off the build with the runbook's Linux recipe.
7. Print the runbook:
   - merge into `develop`, then `main`;
   - tag `1.1.0`;
   - push;
   - publish and install.

## Acceptance criteria

- `dotnet build Enigma.Launchpad.slnx -c Release` is clean, with zero warnings, and
  `dotnet test --solution Enigma.Launchpad.slnx -c Release` is green. The same holds in Debug.
- The built assemblies carry `1.1.0.0` and the informational version `1.1.0+<commit>`.
- `RELEASENOTES.md`'s top section is `1.1.0 — 2026-10-14` and covers the four features, the removed pages, the
  upgrade and every dependency transition. The README callout names 1.1. `SECURITY.md` supports 1.1.x.
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

| Question                         | Chosen                                                                                          | Why                                                                                                                                      | Alternatives rejected                                         |
|----------------------------------|-------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------|
| The version number               | 1.1.0, as asked                                                                                 | The user named it. Nothing saved is lost: the three pages' groups move into a workspace on their own, and older export files still import | 2.0.0 for the removed pages                                   |
| Release-notes shape              | Enigma.GitClient 6.1.0's                                                                        | The house precedent for a later app release; 1.0.0's shape was the first-release one                                                     | The template's *New Features · Fixes* headings                 |
| Dependency refresh               | Microsoft.Extensions.* → 10.0.13 and xunit.v3 → 4.0.2; Avalonia held at 12.1.1                  | Non-coupled patch bumps; the Avalonia set moves with Enigma.Avalonia.Desktop, whose 1.2.0 is built against 12.1.1                        | Bumping Avalonia to 12.1.4; no refresh                        |
| `SECURITY.md`                    | 1.1.x supported, 1.0.x not                                                                      | The policy covers the latest release, as GitClient's table does                                                                          | Supporting both minors                                        |
| Licence audit                    | Not repeated                                                                                    | First release only (`dotnet-release`), and no package was added since                                                                    | Re-auditing every package                                     |
| MSI profile                      | None                                                                                            | Declined at 1.0.0 (`docs/RELEASE.md`); the release's packaging is the Linux installer                                                    | Generating the first profile                                  |
| Tag                              | Bare `1.1.0`, printed                                                                           | Matches `1.0.0`; a run never tags                                                                                                        | `v1.1.0`; tagging in the run                                  |
| Going back to 1.0                | Documented in *Upgrading from 1.0*                                                              | 1.0.0 sets a version 3 file aside and starts empty. The backup is the way back, and the user should know before upgrading                 | Leaving it out                                                |
