# FEATURE-0859 — Release 1.5.0

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-0859-release-1-5-0`
**Run:** feature/2026-11-03-icons-msi-release

## Objective

Cut version **1.5.0** (1.4.0 with the minor version incremented, z already 0), using FEATURE-D4F5 (1.4.0) as the
template and following the `dotnet-release` skill. The release covers this run:

- FEATURE-BF01: the new icon set without the tile;
- FEATURE-E90D: the Windows installer (`buildmsi.ps1` / `buildmsi.bat`, the vendored Enigma.Msi worker).

An item that ended quarantined is left out of the notes. The run makes the in-repo edits; the merges, the tag, the
pushes, the MSI build and the publish are printed for the user to run.

## Context & constraints

- **An app, not a package** (`dotnet-release`, *App vs library*): no `PackageId`, so no `dotnet pack` or push.
- `Directory.Build.props` holds `<Version>1.4.0</Version>` once for the whole solution.
- Tags are bare `X.Y.Z` (`1.0.0` … `1.4.0`); `1.5.0` does not exist yet.
- `docs/RELEASE.md` is generic in `X.Y.Z`: it is followed, not rewritten (FEATURE-E90D writes its installer step).
- The precedent is `RELEASENOTES.md`'s 1.4.0 section: a dated `## X.Y.Z — <date>` heading, a summary list, themed
  sections, *Upgrading from X.Y*, *Dependencies*, *Version*.
- `SECURITY.md` supports only the latest minor line.
- The Avalonia set moves only as a whole, and only together with what Enigma.Avalonia.Desktop is built against.
- No file format changes in this run: `launchpad.json`, `settings.json` and exports are unchanged.

## Steps

1. Check that `git tag --list 1.5.0` is empty.
2. `Directory.Build.props`: `<Version>1.5.0</Version>`.
3. `Directory.Packages.props`: `dotnet list package --outdated`, logged. Non-coupled patch and minor bumps are
   applied; the Avalonia set is held back unless Enigma.Avalonia.Desktop moved with it.
4. `RELEASENOTES.md`: prepend `## 1.5.0 — 2026-11-03`, in 1.4.0's shape: a summary; *The icon without its tile*;
   *The Windows installer*; *Upgrading from 1.4*; *Dependencies*; *Version*. Every path the notes quote is checked
   against the files.
5. `README.md`: the callout becomes *What's new in 1.5*.
6. `SECURITY.md`: 1.5.x supported; 1.4.x no longer. The scope names the Windows installer script if it lists the
   packaging.
7. Pre-flight in Release and Debug, then the version read off the build.
8. Print the runbook: merge into `develop`, then `main`; tag `1.5.0`; push; build the MSI; install.

## Acceptance criteria

- Release and Debug builds are clean with zero warnings; the whole suite shows no new failure in both.
- The built assemblies carry `1.5.0.0` and the informational version `1.5.0+<commit>`.
- `RELEASENOTES.md`'s top section is `1.5.0 — 2026-11-03` and covers every `DONE` item of the run, with quoted paths
  that match the files. The README callout names 1.5. `SECURITY.md` supports 1.5.x.
- Nothing in the dev runs `git tag`, `git push`, a merge into `develop` or `main`, `dotnet pack`, a NuGet push, or
  installs the MSI.

## Out of scope

- NuGet packaging, a committed MSI profile, a licence audit, tagging, merging, pushing.
- Building the 1.5.0 MSI for distribution (the user runs `buildmsi` after merging).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| The version number | 1.5.0 | The spec's rule: x.y.z → x.(y+1).0 | 1.4.1; 2.0.0 |
| Order in the run | Last | The notes describe the icon and the installer, built first | First |
| Phasing | One phase | A routine release | Splitting notes from the version |
| Release-notes shape | 1.4.0's, with GitClient 6.14.0's two themed sections | The repository's own precedent; the sibling release of the same changes | The template's *New Features · Fixes* |
| `SECURITY.md` | 1.5.x supported only | The policy covers the latest line | Supporting 1.4.x too |
| Licence audit | Not repeated for the app; the worker's licences are in its README (FEATURE-E90D) | The worker builds the MSI and does not ship in it | A new audit |
| Committed MSI profile | None | FEATURE-E90D's script generates it per build | `msiProfiles/<App>.1.5.0.msipkg.json` |
| Dependencies | Non-coupled bumps applied; the Avalonia set held with the library | House rule from earlier releases | Bumping everything |
| Tag | Bare `1.5.0`, printed | Matches earlier tags; a run never tags | `v1.5.0` |
