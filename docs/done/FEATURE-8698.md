# FEATURE-8698 — Release 1.6.0

**Item:** FEATURE-8698 — Release 1.6.0
**Branch:** `feature/feature-8698-release-1-6-0`
**Run:** feature/2026-11-04-icons-release

## Summary

Version **1.6.0**: 1.5.0 with the minor version incremented (z was already 0). The release covers this run, whose
items are all `DONE`:

- FEATURE-003B: the icon set that fills the frame.

The version is set once, in `Directory.Build.props`. `buildmsi.ps1` holds no version of its own: it reads that
`<Version>`, so it now builds `artifacts\Enigma.Launchpad.1.6.0.msi` with no edit to the script.

The in-repo edits are made. The merges, the tag, the pushes and the MSI build are printed for the user (below), never
run.

## Files/modules touched

- Modified: `Directory.Build.props` (`<Version>1.6.0</Version>`)
- Modified: `RELEASENOTES.md` (the `1.6.0 — 2026-11-04` section, above 1.5.0's), `README.md` (the *What's new in 1.6*
  callout, which sums up 1.5 after it), `SECURITY.md` (1.6.x supported, 1.5.x no longer)
- Modified: `docs/roadmap.md`, `docs/plan/FEATURE-8698.md`; created `docs/done/FEATURE-8698.md`

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Themed sections | *The icon fills its frame*, then 1.5.0's *Upgrading*, *Dependencies*, *Version* | 1.5.0's shape, with one theme for this release's one change |
| How much larger | "scaled by about 1.45 … spans 224 of the 256 units, where it spanned 155" | The SVG master's header: scale 1.4452, longest side 16 to 240; 224 / 1.4452 = 155 |
| Windows icon cache | A sentence under *Upgrading from 1.5* | A pinned taskbar shortcut can keep a cached icon after an upgrade; the user's first check is the taskbar |
| `buildmsi.ps1` | Unchanged; its version read run against the bumped file | The script derives the version from `Directory.Build.props` by design (its help, `docs/RELEASE.md`) |
| The Avalonia set | Held at 12.1.4 | 12.1.5 is out, but Enigma.Avalonia.Desktop 1.3.0, still the latest, is built against 12.1.4; the set moves only with it |

## Deviations & follow-ups

- The spec asked to "update the new version number in the buildmsi script". The script has none to update: it reads
  `<Version>` from `Directory.Build.props` (`docs/RELEASE.md`, *The Windows installer's identity*: "a release changes
  nothing for the installer"). The bump in `Directory.Build.props` is the update; hard-coding the version in the
  script would add a second source that drifts.
- `Directory.Packages.props` is unchanged: `dotnet list package --outdated` on 2026-11-04 lists only the Avalonia set
  (12.1.4 → 12.1.5: Avalonia, Avalonia.Desktop, Avalonia.Fonts.Inter, Avalonia.Themes.Fluent, Avalonia.Headless,
  Avalonia.Skia), held back with Enigma.Avalonia.Desktop. Core has no update.
- Not done here, as the runbook says: merging into `develop` and `main`, tagging `1.6.0`, pushing, building the
  1.6.0 MSI, installing it.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-003B.md` (0 warnings; 1,028 tests, 1,027 passed, 1 skipped, 0 failed).
- `git tag --list 1.6.0`: empty before the release.
- `buildmsi.ps1`'s version read (its `XmlDocument` lines, run on their own: the script itself runs `git clean -dxf`)
  against the bumped `Directory.Build.props`: valid, `1.6.0`, MSI name `Enigma.Launchpad.1.6.0`.
- `dotnet build Enigma.Launchpad.slnx -c Release --no-incremental`: **0 warnings, 0 errors**.
  `dotnet test --solution Enigma.Launchpad.slnx -c Release`: 1,028 total, 1,027 passed, 1 skipped (the Unix-only
  resolver test), 0 failed.
- `dotnet build Enigma.Launchpad.slnx --no-incremental` (Debug): **0 warnings, 0 errors**. `dotnet test` (Debug): the
  same counts.
- The built version, read with the runbook's Windows recipe on
  `src/Enigma.Launchpad.Desktop/bin/Release/net10.0/Enigma.Launchpad.Core.dll`: product version
  `1.6.0+7ffde7aa3e1ccd3bf9606b60edf10d16b02bb982` (the run branch's tip at build time), file version `1.6.0.0`;
  `Enigma.Launchpad.Desktop.dll`'s assembly version `1.6.0.0`.
- Every path the new release notes quote exists in the repository.
- Nothing in this dev ran `git tag`, `git push`, a merge into `develop` or `main`, `buildmsi`, `dotnet pack`, a NuGet
  push, or installed an MSI.
- Fix budget: 0 cycles used.

## Runbook (printed, for the user to run)

1. Pre-flight: `dotnet build Enigma.Launchpad.slnx -c Release`;
   `dotnet test --solution Enigma.Launchpad.slnx -c Release`.
2. `git switch develop && git merge --no-ff feature/2026-11-04-icons-release`, then
   `git switch main && git merge --no-ff develop`.
3. `git tag 1.6.0` (bare, like `1.0.0` to `1.5.0`).
4. `git push origin develop main` and `git push origin 1.6.0`.
5. On `main`, with every new file committed (it runs `git clean -dxf`): `packaging\windows\buildmsi.bat`, which leaves
   `artifacts\Enigma.Launchpad.1.6.0.msi`. Install it; it replaces 1.5.0. On Linux, `packaging/linux/install.sh`.
6. Post-release checks as in `docs/RELEASE.md` §7: the new icon in the launcher and the taskbar, one *Enigma
   Launchpad* at 1.6.0 in *Installed apps*, About shows 1.6.0 with the tagged commit, the shortcuts are intact.

## Documentation sweep

- `RELEASENOTES.md`, `README.md` and `SECURITY.md` are this dev's own edits. `docs/RELEASE.md` is generic in `X.Y.Z`
  and already says the installer takes its version from `Directory.Build.props`; it needed nothing.
