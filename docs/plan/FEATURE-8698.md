# FEATURE-8698 — Release 1.6.0

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-8698-release-1-6-0`
**Run:** feature/2026-11-04-icons-release

## Objective

Cut version **1.6.0** (1.5.0 with the minor version incremented, z already 0), using FEATURE-0859 (1.5.0) as the
template and following the `dotnet-release` skill. The release covers this run:

- FEATURE-003B: the icon set that fills the frame.

An item that ended quarantined is left out of the notes. The run makes the in-repo edits; the merges, the tag, the
pushes, the MSI build and the install are printed for the user to run.

## Context & constraints

- **An app, not a package** (`dotnet-release`, *App vs library*): no `PackageId`, so no `dotnet pack` or push.
- `Directory.Build.props` holds `<Version>1.5.0</Version>` once for the whole solution.
- **The buildmsi script carries no version of its own.** `buildmsi.ps1` reads `<Version>` from
  `Directory.Build.props` and names the MSI `Enigma.Launchpad.X.Y.Z.msi` from it; `docs/RELEASE.md` (*The Windows
  installer's identity*) and the 1.5.0 notes state that a release changes nothing for the installer. The spec's
  "update the new version number in the buildmsi script" is met by the `Directory.Build.props` bump, proven by
  running the script's own version read against the bumped file.
- Tags are bare `X.Y.Z` (`1.0.0` … `1.5.0`); `1.6.0` does not exist yet.
- `docs/RELEASE.md` is generic in `X.Y.Z`: it is followed, not rewritten.
- The precedent is `RELEASENOTES.md`'s 1.5.0 section: a dated `## X.Y.Z — <date>` heading, a summary list, themed
  sections, *Upgrading from X.Y*, *Dependencies*, *Version*.
- `SECURITY.md` supports only the latest minor line.
- The Avalonia set moves only as a whole, and only together with what Enigma.Avalonia.Desktop is built against.
- No file format changes in this run: `launchpad.json`, `settings.json` and exports are unchanged.

## Steps

1. Check that `git tag --list 1.6.0` is empty.
2. `Directory.Build.props`: `<Version>1.6.0</Version>`.
3. Run `buildmsi.ps1`'s version read (the `XmlDocument` lines, not the script, which runs `git clean -dxf`) against
   the bumped file: it must yield `1.6.0` and the name `Enigma.Launchpad.1.6.0`.
4. `Directory.Packages.props`: `dotnet list package --outdated`, logged. Non-coupled patch and minor bumps are
   applied; the Avalonia set is held back unless Enigma.Avalonia.Desktop moved with it.
5. `RELEASENOTES.md`: prepend `## 1.6.0 — 2026-11-04`, in 1.5.0's shape: a summary; *The icon fills its frame*;
   *Upgrading from 1.5*; *Dependencies*; *Version*. Every path the notes quote is checked against the files.
6. `README.md`: the callout becomes *What's new in 1.6*, then 1.5 in one sentence.
7. `SECURITY.md`: 1.6.x supported; 1.5.x no longer.
8. Pre-flight in Release and Debug, then the version read off the build.
9. Print the runbook: merge into `develop`, then `main`; tag `1.6.0`; push; build the MSI; install.

## Acceptance criteria

- Release and Debug builds are clean with zero warnings; the whole suite shows no new failure in both.
- The built assemblies carry `1.6.0.0` and the informational version `1.6.0+<commit>`.
- `buildmsi.ps1`'s version read yields `1.6.0`; the script itself is unchanged.
- `RELEASENOTES.md`'s top section is `1.6.0 — 2026-11-04` and covers every `DONE` item of the run, with quoted paths
  that match the files. The README callout names 1.6. `SECURITY.md` supports 1.6.x.
- Nothing in the dev runs `git tag`, `git push`, a merge into `develop` or `main`, `buildmsi`, `dotnet pack`, a NuGet
  push, or installs the MSI.

## Out of scope

- NuGet packaging, a committed MSI profile, a licence audit, tagging, merging, pushing.
- Building the 1.6.0 MSI for distribution (the user runs `buildmsi` after merging).
- Editing `buildmsi.ps1`.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| The version number | 1.6.0 | The spec's rule: x.y.z → x.(y+1).z, z = 0; 1.5.0 is tagged | 1.5.1; 2.0.0 |
| "Update the new version number in the buildmsi script" | No edit to the script; the `Directory.Build.props` bump is the update, proven by the script's own version read | The script derives the version from `Directory.Build.props` by design, documented in its help, `docs/RELEASE.md` and the 1.5.0 notes | Hard-coding `1.6.0` in `buildmsi.ps1`: a second source of truth that drifts from the build's version |
| Order in the run | Last | The notes describe the icon, built first | First |
| Phasing | One phase | A routine release | Splitting notes from the version |
| Release-notes shape | 1.5.0's | The repository's own precedent | The template's *New Features · Fixes* |
| README callout | 1.6 first, then 1.5 in one sentence | 1.5.0's callout did the same for 1.4 | Dropping 1.5 |
| `SECURITY.md` | 1.6.x supported only | The policy covers the latest line | Supporting 1.5.x too |
| Licence audit | Not repeated | No new dependency; the artwork is the project's own | A new audit |
| Committed MSI profile | None | `buildmsi.ps1` generates it per build | `msiProfiles/<App>.1.6.0.msipkg.json` |
| Dependencies | Non-coupled bumps applied; the Avalonia set held with the library | House rule from earlier releases | Bumping everything |
| Release date | 2026-11-04 | The run's date | — |
| Tag | Bare `1.6.0`, printed | Matches earlier tags; a run never tags | `v1.6.0` |
