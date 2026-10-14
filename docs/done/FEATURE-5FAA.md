# FEATURE-5FAA — Release 1.1.0

**Item:** FEATURE-5FAA — Release 1.1.0
**Branch:** `feature/feature-5faa-release-1-1-0`
**Run:** bugfix/2026-10-14-release-1-1-0

## Summary

Version **1.1.0** is cut in the repository. The merges, the tag, the pushes and the publish are left for the user to
run. The commands are in `docs/RELEASE.md` and in the run's report.

- `Directory.Build.props`: `<Version>1.1.0</Version>` (was `1.0.0`).
  - The built assemblies carry `1.1.0.0`.
  - Their informational version is `1.1.0+<commit>`.
- `Directory.Packages.props`:
  - the Microsoft.Extensions packages 10.0.12 → 10.0.13, as a set;
  - xunit.v3 4.0.1 → 4.0.2;
  - the Avalonia set held at 12.1.1.
- `RELEASENOTES.md` — `## 1.1.0 — 2026-10-14` prepended, in Enigma.GitClient 6.1.0's shape:
  - a summary list;
  - *Workspaces*, *Documents*, *Commands as applications*, *Drag and drop*;
  - *Changes*: the three pages gone, folders and links, the Profiles page summary, export and import with workspaces;
  - *Upgrading from 1.0*: the move into a *Shortcuts* workspace, the version 3 file and its `launchpad.v2.backup.json`
    copy, old exports still imported, `settings.json` unchanged, and how to go back to 1.0;
  - *Dependencies*, *Version*.
- `README.md` — the callout is *What's new in 1.1*.
- `SECURITY.md` — 1.1.x supported, 1.0.x not.

## Files / modules touched

**Created**

- `docs/done/FEATURE-5FAA.md`

**Modified**

- `Directory.Build.props` — the version
- `Directory.Packages.props` — five package versions
- `RELEASENOTES.md` — the 1.1.0 section
- `README.md` — the what's-new callout
- `SECURITY.md` — the supported-versions table
- `docs/roadmap.md`, `docs/plan/FEATURE-5FAA.md` — statuses

## Dependency refresh

`dotnet list package --outdated`, 2026-10-14:

| Package                                                            | Before  | After   | Note                                                                                             |
|--------------------------------------------------------------------|---------|---------|--------------------------------------------------------------------------------------------------|
| Microsoft.Extensions.DependencyInjection.Abstractions              | 10.0.12 | 10.0.13 | Core                                                                                             |
| Microsoft.Extensions.Logging.Abstractions                          | 10.0.12 | 10.0.13 | Core                                                                                             |
| Microsoft.Extensions.Hosting                                       | 10.0.12 | 10.0.13 | Desktop                                                                                          |
| Microsoft.Extensions.DependencyInjection                           | 10.0.12 | 10.0.13 | Core's tests only                                                                                |
| xunit.v3                                                           | 4.0.1   | 4.0.2   | both test projects                                                                               |
| Avalonia, .Desktop, .Themes.Fluent, .Fonts.Inter, .Headless, .Skia | 12.1.1  | 12.1.1  | **held back**: 12.1.4 is out; the set moves with Enigma.Avalonia.Desktop 1.2.0, still its latest |

`project.assets.json` resolves Microsoft.Extensions.Hosting 10.0.13 and xunit.v3 4.0.2.

No package was added since 1.0.0, and the bumps are patch releases of packages already audited as MIT. So
FEATURE-10CE's runtime licence audit still holds and was not repeated (`dotnet-release`: first release only).

## Deviations & follow-ups

- **Deviation — the release-notes wording on the backup.** The plan said the 1.0 file is copied before the first
  save. The store actually copies it when 1.1 first **reads** a version 2 file (`JsonLaunchpadStore.BackUp`, called
  from the load). The notes say that.
- **Follow-ups, none blocking the release:**
  - Run the suite on Windows before announcing the release. This run is on Linux, and BUG-10AB's portable tests were
    not run on Windows.
  - Enable **Private vulnerability reporting** in the GitHub repository (carried from 1.0.0), if not yet done.
  - The Avalonia set: move it to 12.1.4 with the next Enigma.Avalonia.Desktop.
  - Showing the version inside the application (carried from 1.0.0).
- **`main` and `develop`:** `main` holds the `1.0.0` tag on a merge of `develop` (6499136) that `develop` does not
  contain. Its content equals `develop` at 46887fd, so merging `develop` into `main` is conflict-free.
- Fix budget: none used. The first build and test runs were green.
- **Documentation sweep:**
  - README: the callout, as above.
  - `docs/RELEASE.md`: generic in `X.Y.Z`, and still true. Its MSI note names 1.0.0 as the release that declined
    it, which stays accurate.
  - Nothing else names the version.
- Line endings: every touched file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx -c Release` — 0 warnings, 0 errors.
  `dotnet test --solution Enigma.Launchpad.slnx -c Release` — total 912, 905 passed, 0 failed, 7 skipped.
- `dotnet build Enigma.Launchpad.slnx` — 0 warnings, 0 errors.
  `dotnet test --solution Enigma.Launchpad.slnx` — total 912, 905 passed, 0 failed, 7 skipped.
- The built version, read with the runbook's Linux recipe:
  - `strings src/Enigma.Launchpad.Desktop/bin/Release/net10.0/Enigma.Launchpad.Core.dll | grep -m1 -o '1\.1\.0+[0-9a-f]*'`
    prints `1.1.0+140243b4339100296abd5ee5d0b9cda7470fa666`, the run branch's tip at build time;
  - the file version is `1.1.0.0`.
- Nothing in this dev ran `git tag`, `git push`, a merge into `develop` or `main`, `dotnet pack` or
  `dotnet nuget push`.

## Decisions taken at build time

| Question                              | Chosen                                                                                   | Why                                                                                                          |
|---------------------------------------|------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------|
| Themed sections in the notes          | *Workspaces*, *Documents*, *Commands as applications*, *Drag and drop*                   | One per feature since 1.0.0 (FEATURE-1C0D, 1FA9, 1F8C, 23F9), in the order a user meets them                  |
| What goes under *Changes*             | The removed pages, the folders/links naming, the Profiles summary, export and import     | What a 1.0 user finds different, as opposed to what is new                                                    |
| Going back to 1.0                     | Quit, copy `launchpad.v2.backup.json` over `launchpad.json`, then start 1.0              | 1.0.0 refuses format version 3 (`format version 3 is not supported by this build`) and would start empty     |
| Version 1 export files                | Named as still imported; 1.1's version 2 exports named as refused by 1.0                 | 1.0.0's `ShortcutExchange.CurrentVersion` is 1 and it reads no other                                         |
| The callout                           | The four features, plus that the shortcuts move on their own                             | The one-line highlight a 1.0 user needs before upgrading                                                      |
