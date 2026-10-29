# FEATURE-D4F5 — Release 1.4.0

**Status:** TODO
**Type:** FEATURE
**Branch:** `feature/feature-d4f5-release-1-4-0`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Cut version **1.4.0** (1.3.0 with the minor version incremented). The release covers this run:

- FEATURE-424E: packages updated;
- FEATURE-7298: the icon's SVG master in `packaging/icon/`, eight Linux icon sizes and no scalable one;
- FEATURE-7F4A: `app.ico` in the output directory;
- FEATURE-B7DD: publish scripts;
- FEATURE-2961: the About dialog;
- FEATURE-4757: the safety net for unhandled errors.

The run makes the in-repo edits. The merges, the tag, the pushes and the publish are printed for the user to run.

## Context & constraints

- **An app, not a package** (`dotnet-release`, *App vs library*): no `PackageId`, so no `dotnet pack` or push.
- `Directory.Build.props` holds `<Version>1.3.0</Version>` once for the whole solution.
- Tags are bare `X.Y.Z` (`1.0.0`, `1.1.0`, `1.2.0`, `1.3.0`).
- `docs/RELEASE.md` is generic in `X.Y.Z`: it is followed, not rewritten.
- The precedent is the repository's own 1.3.0 and 1.2.0 sections of `RELEASENOTES.md`:
  - a dated `## X.Y.Z — <date>` heading, then a summary list;
  - themed sections, *Upgrading from X.Y*, *Dependencies*, *Version*.
- 1.3.0 (FEATURE-44CA, FEATURE-EB8F) already shipped the new icon, its 96 px Linux size and Microsoft.Extensions
  10.0.14. Its release notes stay as they are.
- No file format changes in this run: `launchpad.json`, `settings.json` and exports are unchanged.

## Steps

1. `Directory.Build.props`: `<Version>1.4.0</Version>`.
2. `Directory.Packages.props`: re-check `dotnet list package --outdated`. FEATURE-424E left nothing outdated;
   bump anything published since.
3. `RELEASENOTES.md`: prepend `## 1.4.0 — 2026-10-29`, in 1.3.0's shape:
   - a summary;
   - *About*;
   - *When something goes wrong*;
   - *Publishing and installing* (publish scripts, `app.ico` in the output, the SVG master, eight Linux icon
     sizes and no scalable one);
   - *Upgrading from 1.3* (nothing converted; the scalable icon 1.3 installed is left behind);
   - *Dependencies*;
   - *Version*.
4. `README.md`: the callout becomes *What's new in 1.4*.
5. `SECURITY.md`: 1.4.x supported; earlier lines not.
6. Pre-flight in Release and Debug, then the version read off the build.
7. Print the runbook: merge into `develop`, then `main`; tag `1.4.0`; push; publish and install.

## Acceptance criteria

- Release and Debug builds are clean with zero warnings; the whole suite is green in both.
- The built assemblies carry `1.4.0.0` and the informational version `1.4.0+<commit>`.
- `RELEASENOTES.md`'s top section is `1.4.0 — 2026-10-29` and covers every run item. The README callout
  names 1.4. `SECURITY.md` supports 1.4.x.
- Nothing in the dev runs `git tag`, `git push`, a merge into `develop` or `main`, `dotnet pack` or a NuGet push.

## Out of scope

- NuGet packaging, an MSI profile, a licence audit, tagging, merging into `develop` or `main`, pushing.

## Spec items with nothing to do (checked when the run was planned)

- **Remove the splash screen:** there is none. `App` shows `MainWindow` directly.
- **Remove net8:** no project targets net8. Every project is `net10.0` only.
- **Linux install and uninstall scripts:** they exist (FEATURE-200F) and match GitClient's line for line,
  apart from the scalable icon FEATURE-44CA added, which FEATURE-7298 drops.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| The version number | 1.4.0 | The spec's rule: x.y.z → x.(y+1).0. The run was planned from 1.2.0, but 1.3.0 was cut meanwhile (FEATURE-EB8F), and the user chose to keep it | Re-cutting 1.3.0 over FEATURE-EB8F; 1.3.1 |
| Phasing | One phase | A routine release: version, notes, callout, runbook | Separate notes and version phases |
| Release-notes shape | 1.3.0's | The repository's own precedent | The template's *New Features · Fixes* |
| `SECURITY.md` | 1.4.x supported only | The policy covers the latest line, as at 1.3.0 | Supporting 1.3.x too |
| Licence audit | Not repeated | First release only; Enigma.Core (transitive) already came with Enigma.Avalonia.Desktop 1.2.0 | Re-auditing |
| Tag | Bare `1.4.0`, printed | Matches earlier tags; a run never tags | `v1.4.0` |
