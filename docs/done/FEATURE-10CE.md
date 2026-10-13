# FEATURE-10CE — Release 1.0.0

**Item:** FEATURE-10CE — Release 1.0.0
**Branch:** `feature/feature-10ce-release-1-0-0`
**Run:** vibe/2026-10-13-linux-installer-release

## Summary

Version **1.0.0** is cut in the repository. The merges, the tag and the pushes are left for the user to run; the
commands are in `docs/RELEASE.md` and in the run's report.

- `Directory.Build.props`: `<Version>1.0.0</Version>` (was `0.1.0`), once for the whole solution.
  - The built assemblies carry `1.0.0.0`.
  - Their informational version is `1.0.0+<commit>`.
- `RELEASENOTES.md` (it was empty):
  - `# Release notes`, then `## 1.0.0 — 2026-10-13`, in Enigma.GitClient's app shape.
  - A positioning paragraph, then themed sections: *Shortcuts and groups*, *Tiles*, *Icons*, *Profiles*,
    *Export and import*, *Settings and your data*, *Installing*, *Compatibility*, *Version*.
- `README.md`:
  - the *What's new in 1.0* callout under the intro;
  - the layout table's `docs/` row names the runbook.
- `docs/RELEASE.md` — the `dotnet-release` runbook, adapted to an app after Enigma.GitClient's:
  - pre-flight in Release;
  - the version read off the build (Linux `strings`, Windows `VersionInfo`), since the app shows it nowhere;
  - merge into `develop` then `main`, tag `X.Y.Z`, push;
  - publish, then install with the Linux installer, then verify;
  - why there is no pack, no NuGet push and no MSI profile.
- `SECURITY.md`:
  - GitHub private vulnerability reporting, with 1.0.x supported;
  - a scope naming what is sensitive here: starting programs, folders, terminals and addresses; fetching website
    icons; reading imported files and their images; the data directory; the installer.

## Files / modules touched

**Created**

- `docs/RELEASE.md`
- `SECURITY.md`

**Modified**

- `Directory.Build.props` — `<Version>1.0.0</Version>`
- `RELEASENOTES.md` — the 1.0.0 section
- `README.md` — the what's-new callout; the `docs/` row of the layout table

## Runtime licence audit

The audit covers what a published build redistributes:

- every package in the Desktop project's `project.assets.json` for `net10.0` that has runtime or native assets,
  each licence read from the package's own nuspec or licence file;
- the .NET runtime itself, for a self-contained build.

| Component                                                                                                                                                            | Licence                            | Ships                                                                                     |
|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------|-------------------------------------------------------------------------------------------|
| .NET runtime (self-contained builds)                                                                                                                                 | MIT                                | Linux installer default                                                                   |
| Avalonia 12.1.1 and its platform packages (Desktop, X11, Win32, Native, FreeDesktop, FreeDesktop.AtSpi, Skia, HarfBuzz, Themes.Fluent, Remote.Protocol, Fonts.Inter) | MIT                                | all builds                                                                                |
| `Avalonia.Angle.Windows.Natives` (ANGLE)                                                                                                                             | BSD-3-Clause (licence file)        | Windows builds                                                                            |
| SkiaSharp 3.119.4 and its natives                                                                                                                                    | MIT                                | all builds                                                                                |
| HarfBuzzSharp 8.3.1.3 and its natives                                                                                                                                | MIT                                | all builds                                                                                |
| Inter (the font, inside `Avalonia.Fonts.Inter`)                                                                                                                      | SIL OFL 1.1                        | all builds                                                                                |
| CommunityToolkit.Mvvm 8.4.2                                                                                                                                          | MIT                                | all builds                                                                                |
| Microsoft.Extensions.* 10.0.12 (Hosting, Configuration, DependencyInjection, Logging, Options, FileProviders, …)                                                     | MIT                                | all builds                                                                                |
| System.Diagnostics.EventLog 10.0.12                                                                                                                                  | MIT                                | all builds                                                                                |
| Enigma.Avalonia.Desktop 1.2.0; Enigma.Core, Enigma.Icons, Enigma.Icons.Avalonia, Enigma.Icons.Phosphor 1.0.0                                                         | MIT (`LICENSE.md`)                 | all builds                                                                                |
| Phosphor Icons artwork (inside Enigma.Icons.Phosphor)                                                                                                                | MIT (its `THIRD-PARTY-NOTICES.md`) | all builds                                                                                |
| BouncyCastle.Cryptography 2.6.2 (through Enigma.Core)                                                                                                                | MIT                                | all builds                                                                                |
| MicroCom.Runtime 0.11.6, Tmds.DBus.Protocol 0.94.1, Microsoft.IO.RecyclableMemoryStream 3.0.1                                                                        | MIT                                | all builds                                                                                |
| AvaloniaUI.DiagnosticsSupport 2.2.3                                                                                                                                  | —                                  | **not shipped**: excluded from non-Debug builds (none in the `linux-x64` Release publish) |
| Avalonia.BuildServices 11.3.2                                                                                                                                        | MIT                                | not shipped: build-time only                                                              |

Every licence permits redistribution in binary form. The application's own licence is MIT (`LICENSE.md`).

## Deviations & follow-ups

- **Follow-ups, none blocking the release:**
  - Enable **Private vulnerability reporting** in the GitHub repository's *Settings → Security*. `SECURITY.md`
    relies on it.
  - Show the version inside the application, for example a line on the Settings page. Today the runbook reads it
    off the build.
  - A Windows installer (the MSI profile `dotnet-release` offers for apps), when Windows packaging is wanted. The
    first profile's `upgradeCode` is then reused forever.
  - Check the Linux installer on a real desktop before announcing the release. FEATURE-200F could not open the
    window in the minimal WSL distro (no fontconfig there).
- No dependency refresh, as planned.
- `main` holds only the initial commit and is an ancestor of the run branch, so the runbook's merge into `main` is a
  plain merge. There is no local `main` branch yet; `git switch main` creates it from `origin/main`.
- Fix budget: none used. The first build and test runs were green.
- Documentation sweep: the README callout and its layout row, as above. Nothing else names the version.
- Line endings: every touched file is LF, per `.gitattributes`.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx -c Release` — 0 warnings, 0 errors.
  `dotnet test --solution Enigma.Launchpad.slnx -c Release` — 710 passed, 0 failed, 0 skipped.
- `dotnet build Enigma.Launchpad.slnx` — 0 warnings, 0 errors.
  `dotnet test --solution Enigma.Launchpad.slnx` — 710 passed, 0 failed, 0 skipped.
- The built version, read with the runbook's two recipes:
  - `strings …/Enigma.Launchpad.Core.dll | grep -m1 -o '1\.0\.0+[0-9a-f]*'` (in WSL) prints
    `1.0.0+502ac2709a4783ffceca896f2e8adab300ac0809`, the run branch's tip at build time;
  - `(Get-Item …).VersionInfo` reports `ProductVersion 1.0.0+502ac27…` and `FileVersion 1.0.0.0`.
- Nothing in this dev ran `git tag`, `git push`, `dotnet pack` or `dotnet nuget push`.

## Decisions taken at build time

| Question                           | Chosen                                                                        | Why                                                                                                  |
|------------------------------------|-------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------|
| The release-notes heading          | `## 1.0.0 — 2026-10-13` under `# Release notes`                               | Enigma.GitClient's `version — date` style                                                            |
| Release-notes content              | What the application does today, by theme, plus *Compatibility* and *Version* | A first release describes the whole product; the README and the completion records are the source    |
| Platform caveat                    | Programs' own icons are named as Windows-only in *Compatibility*              | `ProgramIconReader` reads only on Windows; elsewhere a tile shows its kind's icon                    |
| Reading the version in the runbook | Off the build, with a Linux and a Windows recipe                              | The application shows its version nowhere, and adding that was out of scope                          |
| Runbook branch flow                | release branch → `develop` → `main`, then tag `main`                          | How this repository integrates: every run is merged into `develop`, and `main` is the release branch |
| Runtime identifiers in the runbook | `linux-x64`, plus `linux-arm64`, `win-x64`, `osx-arm64` named                 | The README names macOS as supported                                                                  |
