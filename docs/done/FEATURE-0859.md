# FEATURE-0859 — Release 1.5.0

**Item:** FEATURE-0859 — Release 1.5.0
**Branch:** `feature/feature-0859-release-1-5-0`
**Run:** feature/2026-11-03-icons-msi-release

## Summary

Version **1.5.0**: 1.4.0 with the minor version incremented (z was already 0). The release covers this run, whose
items are all `DONE`:

- FEATURE-BF01: the new icon set without the tile;
- FEATURE-E90D: the Windows installer (`buildmsi.ps1` / `buildmsi.bat`, the vendored Enigma.Msi 1.7.0 worker).

The in-repo edits are made. The merges, the tag, the pushes and the MSI build are printed for the user (below), never
run.

## Files/modules touched

- Modified: `Directory.Build.props` (`<Version>1.5.0</Version>`)
- Modified: `RELEASENOTES.md` (the `1.5.0 — 2026-11-03` section, above 1.4.0's), `README.md` (the *What's new in 1.5*
  callout, which sums up 1.4 after it), `SECURITY.md` (1.5.x supported, 1.4.x no longer; the scope names the Windows
  installer script)
- Modified: `docs/roadmap.md`, `docs/plan/FEATURE-0859.md`; created `docs/done/FEATURE-0859.md`

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Themed sections | *The icon without its tile*, *The Windows installer*, then 1.4.0's *Upgrading*, *Dependencies*, *Version* | GitClient 6.14.0's sections for the same two changes, in Launchpad 1.4.0's shape |
| The callout | 1.5 first, then 1.4 in one sentence | GitClient 6.14.0's callout does the same for the release before |
| `SECURITY.md` *Scope* | Adds "the Windows installer script" | FEATURE-E90D's script runs `git clean -dxf` and builds what users install; the line listed the other packaging scripts |
| The Avalonia set | Held at 12.1.4 | 12.1.5 is out, but Enigma.Avalonia.Desktop 1.3.0, the latest, is built against 12.1.4; the set moves only with it |

## Deviations & follow-ups

- `Directory.Packages.props` is unchanged: `dotnet list package --outdated` on 2026-11-03 lists only the Avalonia set
  (12.1.4 → 12.1.5: Avalonia, Avalonia.Desktop, Avalonia.Fonts.Inter, Avalonia.Themes.Fluent, Avalonia.Headless,
  Avalonia.Skia), held back with Enigma.Avalonia.Desktop. Core has no update.
- Not done here, as the runbook says: merging into `develop` and `main`, tagging `1.5.0`, pushing, building the
  1.5.0 MSI, installing it.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-BF01.md` (0 warnings; 1,025 tests, 1,024 passed, 1 skipped, 0 failed).
- `git tag --list 1.5.0`: empty before the release.
- `dotnet build Enigma.Launchpad.slnx -c Release --no-incremental`: **0 warnings, 0 errors**.
  `dotnet test --solution Enigma.Launchpad.slnx -c Release`: 1,028 total, 1,027 passed, 1 skipped (the Unix-only
  resolver test), 0 failed.
- `dotnet build Enigma.Launchpad.slnx --no-incremental` (Debug): **0 warnings, 0 errors**. `dotnet test` (Debug): the
  same counts.
- The built version, read with the runbook's Windows recipe on
  `src/Enigma.Launchpad.Desktop/bin/Release/net10.0/Enigma.Launchpad.Core.dll`: product version
  `1.5.0+7f9cbd60cffd2852ca52cd005b8e1343a0deb7b5` (the run branch's tip at build time), file and assembly version
  `1.5.0.0`.
- Every path the new release notes quote exists in the repository.
- Nothing in this dev ran `git tag`, `git push`, a merge into `develop` or `main`, `dotnet pack`, a NuGet push, or
  installed an MSI.
- Fix budget: 0 cycles used.

## Runbook (printed, for the user to run)

1. Pre-flight: `dotnet build Enigma.Launchpad.slnx -c Release`;
   `dotnet test --solution Enigma.Launchpad.slnx -c Release`.
2. `git switch develop && git merge --no-ff feature/2026-11-03-icons-msi-release`, then
   `git switch main && git merge --no-ff develop`.
3. `git tag 1.5.0` (bare, like `1.0.0` to `1.4.0`).
4. `git push origin develop main` and `git push origin 1.5.0`.
5. On `main`, with every new file committed (it runs `git clean -dxf`): `packaging\windows\buildmsi.bat`, which leaves
   `artifacts\Enigma.Launchpad.1.5.0.msi`. Install it; it replaces 1.4.0. On Linux, `packaging/linux/install.sh`.
6. Post-release checks as in `docs/RELEASE.md` §7: the new icon in the launcher and the taskbar, one *Enigma
   Launchpad* at 1.5.0 in *Installed apps*, About shows 1.5.0 with the tagged commit, the shortcuts are intact.

## Documentation sweep

- `RELEASENOTES.md`, `README.md` and `SECURITY.md` are this dev's own edits. `docs/RELEASE.md` is generic in `X.Y.Z`,
  and its installer step is FEATURE-E90D's; it needed nothing.
