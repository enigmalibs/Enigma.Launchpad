# FEATURE-10CE — Release 1.0.0

**Status:** TODO
**Type:** FEATURE
**Branch:** `feature/feature-10ce-release-1-0-0`
**Run:** vibe/2026-10-13-linux-installer-release

## Objective

Cut version **1.0.0**: the version stated once for the whole solution, dated and complete release notes, the
README's what's-new callout, a release runbook in `docs/RELEASE.md`, a `SECURITY.md`, the first-release runtime
licence audit, and the merge/tag/push commands printed for the user to run.

## Context & constraints

- **An app, not a package.** No project has a `PackageId`; per `dotnet-release`'s *App vs library* rule the version
  bump, the release notes, the README callout and the tag apply, and `dotnet pack` / `dotnet nuget push` and the
  packable-library prerequisites do not.
- `Directory.Build.props` already holds `<Version>0.1.0</Version>` with the comment that Core and Desktop ship as one
  application. The built assemblies read `0.1.0+<commit>`. The application shows its version nowhere.
- `RELEASENOTES.md` exists and is empty. Enigma.GitClient's notes — the house precedent for an app — use
  `# Release notes`, then `## X.Y.Z — <date>` sections with themed sub-sections and a *Compatibility* one.
- The repository has no tags, a GitHub remote `origin`, `main` holding only the initial commit (an ancestor of
  `develop`), and work integrated on `develop` (`origin/HEAD`).
- A run never tags, pushes or merges into `develop`/`main`: those commands are printed.
- FEATURE-200F (the Linux installer) lands first in this run and distributes binaries — the runtime licence audit
  belongs to this release.
- `docs/RELEASE.md` and `SECURITY.md` do not exist: the `dotnet-release` templates are created, not diffed.

## Steps

1. `Directory.Build.props` — `<Version>1.0.0</Version>`.
2. `RELEASENOTES.md` — `# Release notes`, then `## 1.0.0 — 2026-10-13`: a positioning paragraph, themed sections
   covering what the application does today (categories and groups, tiles and their actions, icons, profiles,
   export and import, settings, your data, installing), and *Compatibility* (Windows, Linux, macOS; .NET 10).
3. `README.md` — the what's-new callout under the intro.
4. `docs/RELEASE.md` — the `dotnet-release` runbook adapted to an app, after Enigma.GitClient's: pre-flight build and
   test in Release, the version read off the build, merge into `develop` then `main`, tag `X.Y.Z`, push, publish,
   install on Linux, verify; no pack or push to NuGet and why; why there is no MSI profile.
5. `SECURITY.md` — from the template, for a public repository whose app starts programs, fetches website icons and
   imports files.
6. The runtime licence audit, recorded in the completion doc: every package the published application
   redistributes and its licence, read from the package.
7. Print the runbook for the user.

## Acceptance criteria

- `dotnet build Enigma.Launchpad.slnx -c Release` is clean with zero warnings and
  `dotnet test --solution Enigma.Launchpad.slnx -c Release` is green (and likewise in Debug).
- The built assemblies carry version `1.0.0` (`1.0.0.0`, informational `1.0.0+<commit>`).
- `RELEASENOTES.md` carries one dated `1.0.0` section covering the release; the README has the callout;
  `docs/RELEASE.md` and `SECURITY.md` exist and every command in the runbook names this repository's real paths.
- Nothing in the dev runs `git tag`, `git push`, `dotnet pack` or `dotnet nuget push`.

## Out of scope

- NuGet packaging and publishing.
- A Windows MSI profile.
- A dependency refresh.
- Showing the version inside the application.
- Tagging, merging, pushing.

## Decisions taken autonomously

| Question                       | Chosen                                                        | Why                                                                                                     | Alternatives rejected                                         |
|--------------------------------|---------------------------------------------------------------|---------------------------------------------------------------------------------------------------------|---------------------------------------------------------------|
| Where the version lives        | `Directory.Build.props`, solution-wide (already there)        | Core and Desktop are one shipped application                                                            | Per-project `<Version>`                                       |
| Release-notes shape            | Enigma.GitClient's app shape: dated section, themed sub-sections, *Compatibility* | The house precedent for an application; the template's *Feature overview* shape is written for a library | The template's first-release variant                          |
| Tag                            | Bare `1.0.0`, printed                                         | No existing tag to match; a run never tags                                                              | `v1.0.0`; tagging inside the run                              |
| MSI profile                    | None this release                                             | Windows installer infrastructure nobody asked for; this release's packaging is the Linux installer      | Generating one                                                |
| `SECURITY.md`                  | Added from the template                                       | The repository is public on GitHub, and the app starts programs, reaches the network and parses imported files | Skipping it                                                   |
| Dependency refresh             | None                                                          | First release; the coupled Avalonia set is pinned with Enigma.Avalonia.Desktop, and unrelated churn does not belong in a release commit | Bumping the non-coupled packages now                          |
| Version shown in the app       | Not added                                                     | Not asked; the runbook reads it off the build instead                                                   | A version line on the Settings page                           |
