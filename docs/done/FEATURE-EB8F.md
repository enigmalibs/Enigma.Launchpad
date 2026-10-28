# FEATURE-EB8F — Release 1.3.0

**Item:** FEATURE-EB8F — Release 1.3.0
**Branch:** `feature/feature-eb8f-release-1-3-0`
**Run:** feature/2026-10-28-new-icons-release

## Summary

Version **1.3.0** is cut in the repository. The merges into `develop` and `main` and the `1.3.0` tag come after the
run, as the user asked. Nothing is pushed.

- `Directory.Build.props`: `<Version>1.3.0</Version>` (was `1.2.0`).
  - The built assemblies carry `1.3.0.0`.
  - Their informational version is `1.3.0+<commit>`.
- `Directory.Packages.props`: Microsoft.Extensions 10.0.13 → 10.0.14. The Avalonia set is held back.
- `RELEASENOTES.md`: `## 1.3.0 — 2026-10-28` prepended, in 1.2.0's shape:
  - a summary list;
  - *Application icon*: the new artwork, where it shows, the ten `.ico` sizes;
  - *Install on Linux*: eight PNG sizes (96 new) and the scalable SVG, all nine removed by the uninstaller;
  - *Upgrading from 1.2*: nothing converted; run the installer again; a cached icon may linger;
  - *Dependencies*, *Version*.
- `README.md`: the callout is *What's new in 1.3*.
- `SECURITY.md`: 1.3.x supported; 1.2.x, 1.1.x and 1.0.x not.

## Files / modules touched

**Created**

- `docs/done/FEATURE-EB8F.md`

**Modified**

- `Directory.Build.props`: the version
- `Directory.Packages.props`: Microsoft.Extensions
- `RELEASENOTES.md`: the 1.3.0 section
- `README.md`: the what's-new callout
- `SECURITY.md`: the supported-versions table
- `docs/roadmap.md`, `docs/plan/FEATURE-EB8F.md`: statuses

## Dependency refresh

`dotnet list package --outdated`, 2026-10-28:

| Package                                                            | Before  | After   | Note                                                        |
|--------------------------------------------------------------------|---------|---------|-------------------------------------------------------------|
| Microsoft.Extensions.DependencyInjection.Abstractions              | 10.0.13 | 10.0.14 | servicing                                                   |
| Microsoft.Extensions.Logging.Abstractions                          | 10.0.13 | 10.0.14 | servicing                                                   |
| Microsoft.Extensions.Hosting                                       | 10.0.13 | 10.0.14 | servicing                                                   |
| Microsoft.Extensions.DependencyInjection (tests)                   | 10.0.13 | 10.0.14 | servicing                                                   |
| Avalonia, .Desktop, .Themes.Fluent, .Fonts.Inter, .Headless, .Skia | 12.1.1  | 12.1.1  | **held back**: 12.1.4 is out; a coupled set                 |
| Enigma.Avalonia.Desktop                                            | 1.2.0   | 1.2.0   | **held back**: 1.3.0 is out; it moves with the Avalonia set |

The restore picked up 10.0.14 (`project.assets.json` in Desktop and in the Core tests). No package was added, so
FEATURE-10CE's runtime licence audit still holds.

## Deviations & follow-ups

- **Deviations:** none. The plan was followed as written.
- **Follow-ups, none blocking the release:**
  - move the Avalonia set to 12.1.4 with Enigma.Avalonia.Desktop 1.3.0, as a dev of its own;
  - run the suite on Windows before announcing the release. On Linux, 7 Windows-only tests are skipped;
  - enable **Private vulnerability reporting** in the GitHub repository, if not yet done (carried from 1.0.0);
  - showing the version inside the application (carried from 1.0.0).
- **`main` and `develop`:** `main` holds the `1.2.0` tag on a merge of `develop` (1c210eb) that `develop` lacks. Its
  content equals `develop` at 203c784, so merging `develop` into `main` is conflict-free.
- Fix budget: none used. The first build and test runs were green.
- **Documentation sweep:**
  - README: the callout, as above. The install table was updated by FEATURE-44CA.
  - `docs/RELEASE.md`: generic in `X.Y.Z`, and still true.
  - Nothing else names the version.
- Line endings: every touched file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx -c Release --no-incremental`: 0 warnings, 0 errors.
  `dotnet test --solution Enigma.Launchpad.slnx -c Release`: total 965, 958 passed, 0 failed, 7 skipped.
- `dotnet build Enigma.Launchpad.slnx --no-incremental`: 0 warnings, 0 errors.
  `dotnet test --solution Enigma.Launchpad.slnx`: total 965, 958 passed, 0 failed, 7 skipped.
- The built version, read with the runbook's Linux recipe:
  - `strings src/Enigma.Launchpad.Desktop/bin/Release/net10.0/Enigma.Launchpad.Core.dll | grep -m1 -o '1\.3\.0+[0-9a-f]*'`
    prints `1.3.0+8da4c747096026a92c1636d6272f48f35d253754`, the run branch's tip at build time;
  - the file version is `1.3.0.0`.
- Nothing in this dev ran `git tag`, `git push`, a merge into `develop` or `main`, or `dotnet pack`.

## Decisions taken at build time

| Question                     | Chosen                                                                     | Why                                                                                         |
|------------------------------|----------------------------------------------------------------------------|---------------------------------------------------------------------------------------------|
| Themed sections in the notes | *Application icon*, then *Install on Linux*                                | What every user sees first, then what only the Linux install adds                           |
| Describing the artwork       | "a rocket in soft blues and green on a dark rounded square"                | The SVG's own colours (#4F7FDB, #6A9BF4, #63C77D on #1E1F22) and its "Soft palette" comment |
| The upgrade on Linux         | Run the installer again; a cached icon may linger until a refresh or login | Icon caches outlive a file swap; the installer refreshes them, but not every shell re-reads |
