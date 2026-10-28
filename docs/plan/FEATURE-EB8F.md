# FEATURE-EB8F — Release 1.3.0

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-eb8f-release-1-3-0`
**Run:** feature/2026-10-28-new-icons-release

## Objective

Cut version **1.3.0**, the next minor after 1.2.0. The release covers the work integrated since the `1.2.0` tag:

- **FEATURE-44CA**, the new application icon: a rocket in the Soft palette on a dark rounded square. The Linux
  installer also adds a 96 px size and a scalable SVG.

The run makes the in-repo edits. The merges into `develop` and `main` and the `1.3.0` tag are not part of the run.
The user asked for them once the run is done.

## Context & constraints

- **An app, not a package** (`dotnet-release`, *App vs library*). There is no `PackageId`, so there is no `dotnet pack`
  and no NuGet push. The version bump, the release notes, the README callout, `SECURITY.md` and the tag apply.
- `Directory.Build.props` holds `<Version>1.2.0</Version>` once for the whole solution.
- The `1.2.0` tag is lightweight. It sits on `main`'s merge of `develop` (1c210eb), whose content equals `develop` at
  203c784. Tags are bare `X.Y.Z`.
- `docs/RELEASE.md` is generic in `X.Y.Z`: it is followed, not rewritten.
- The house precedent is the 1.2.0 section of `RELEASENOTES.md`:
  - a dated `## X.Y.Z — <date>` section, then a short summary list;
  - themed sections, then *Upgrading from X.Y*, *Dependencies* and *Version*.
- `dotnet list package --outdated`, 2026-10-28:
  - **Microsoft.Extensions**, 10.0.13 → 10.0.14: DependencyInjection.Abstractions, Logging.Abstractions, Hosting, and
    DependencyInjection (tests). They are a .NET servicing band, outside the Avalonia set;
  - **the Avalonia set**, 12.1.1 → 12.1.4: Avalonia, .Desktop, .Themes.Fluent, .Fonts.Inter, .Headless and .Skia;
  - **Enigma.Avalonia.Desktop**, 1.2.0 → 1.3.0, coupled with the Avalonia set.
- No data format changes: `launchpad.json`, `settings.json` and exports are untouched by FEATURE-44CA.

## Steps

1. `Directory.Build.props`: `<Version>1.3.0</Version>`.
2. `Directory.Packages.props`: the four Microsoft.Extensions entries → 10.0.14. The Avalonia set and the Enigma
   libraries stay as they are.
3. `RELEASENOTES.md`: prepend `## 1.3.0 — 2026-10-28`, in 1.2.0's shape:
   - a summary list;
   - *Application icon*: the new artwork, where it shows, the installer's 96 px and scalable sizes;
   - *Upgrading from 1.2*: nothing converted; on Linux, run the installer again;
   - *Dependencies*: Microsoft.Extensions moved, the Avalonia set held back;
   - *Version*.
4. `README.md`: the callout becomes *What's new in 1.3*.
5. `SECURITY.md`: 1.3.x supported; 1.2.x, 1.1.x and 1.0.x not.
6. Pre-flight in Release and in Debug, then read the version off the build with the runbook's Linux recipe.

## Acceptance criteria

- `dotnet build Enigma.Launchpad.slnx -c Release` has zero warnings, and
  `dotnet test --solution Enigma.Launchpad.slnx -c Release` is green. The same holds in Debug.
- The built assemblies carry `1.3.0.0` and the informational version `1.3.0+<commit>`.
- `RELEASENOTES.md`'s top section is `1.3.0 — 2026-10-28` and covers the icon, the installer sizes, the upgrade and
  the dependencies. The README callout names 1.3. `SECURITY.md` supports 1.3.x.
- Nothing in the dev runs `git tag`, `git push`, a merge into `develop` or `main`, or `dotnet pack`.

## Out of scope

- Bumping the Avalonia set or Enigma.Avalonia.Desktop.
- NuGet packaging and an MSI profile.
- Showing the version inside the application.
- Tagging and the merges into `develop` and `main`: done after the run, as the user asked. No push.

## Decisions taken autonomously

| Question                | Chosen                                                                   | Why                                                                                                                    | Alternatives rejected                         |
|-------------------------|--------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------|
| The version number      | 1.3.0                                                                    | The user's rule: the minor goes up by one and the patch resets. 1.2.0 → 1.3.0                                          | 1.2.1                                         |
| Phasing                 | One phase, after FEATURE-44CA                                            | A routine release (`dotnet-release`). It describes what the icon dev delivered, so it comes after it                   | Folding the release into the icon dev         |
| Release-notes shape     | 1.2.0's                                                                  | The repository's own precedent                                                                                         | The template's *New Features · Fixes*         |
| Microsoft.Extensions    | 10.0.13 → 10.0.14, all four together                                     | A servicing patch outside the Avalonia set; `dotnet-release` applies non-coupled bumps. The four move as one band      | Holding them back                             |
| The Avalonia set        | Held at 12.1.1 with Enigma.Avalonia.Desktop 1.2.0                        | A coupled set: `dotnet-release` holds it back unless the user opts in. A new control library is a change of its own   | Moving to 12.1.4 and Enigma.Avalonia 1.3.0    |
| `SECURITY.md`           | 1.3.x supported; older lines not                                         | The policy covers the latest release, as at 1.1.0 and 1.2.0                                                            | Supporting 1.2.x too                          |
| Licence audit           | Not repeated                                                             | First release only (`dotnet-release`), and no package was added                                                        | Re-auditing                                   |
| Tag                     | Bare `1.3.0`, lightweight like `1.2.0`, created after the run            | The repository's convention. A run never tags, and the user asked for the tag once the run is done                     | `v1.3.0`; an annotated tag                    |
