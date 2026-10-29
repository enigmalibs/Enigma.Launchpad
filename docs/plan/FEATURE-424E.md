# FEATURE-424E — Update NuGet packages

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-424e-update-packages`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Every package the solution references is at its latest version on nuget.org, so the rest of the run
(and 1.4.0) is built on the current libraries.

## Context & constraints

- `dotnet list Enigma.Launchpad.slnx package --outdated` on 2026-10-29:
  - Microsoft.Extensions.DependencyInjection(.Abstractions), Logging.Abstractions, Hosting: 10.0.13 → 10.0.14;
  - Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter, Avalonia.Headless,
    Avalonia.Skia: 12.1.1 → 12.1.4;
  - Enigma.Avalonia.Desktop: 1.2.0 → 1.3.0.
- `Directory.Packages.props` (CPM) holds every version. The Avalonia set is version-coupled with
  Enigma.Avalonia.Desktop: 1.3.0's nuspec pins Avalonia and Avalonia.Themes.Fluent 12.1.4 and
  CommunityToolkit.Mvvm 8.4.2. Its release notes: no API change, no theme key renamed, removed or re-valued.
- Already latest: CommunityToolkit.Mvvm 8.4.2, Enigma.Icons.Avalonia 1.0.0, AvaloniaUI.DiagnosticsSupport 2.2.3,
  xunit.v3 4.0.2, Microsoft.Testing.Extensions.CodeCoverage 18.12.0.
- Enigma.GitClient moved to the same set in its 6.13.0 line.

## Steps

1. `Directory.Packages.props`: the versions above. Update the Avalonia group's comment so it names
   Enigma.Avalonia.Desktop 1.3.0 and Avalonia 12.1.4.
2. Restore, build in Release and Debug, and run the whole suite.
3. Re-run `dotnet list package --outdated`. It should list nothing.

## Acceptance criteria

- `dotnet list Enigma.Launchpad.slnx package --outdated` reports no update for any project.
- Release and Debug builds: 0 warnings, 0 errors. The whole suite is green (the Unix-only skip excepted).

## Out of scope

- The SDK pin in `global.json` (the SDK, not a package).
- Code changes beyond what a breaking change would force (none is expected).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Which packages | Every outdated one, the Avalonia set included | The spec asks for latest. Enigma.Avalonia.Desktop 1.3.0 now pins 12.1.4, so the set can move, as GitClient's did | Holding Avalonia at 12.1.1, as 1.2.0 did |
| Pre-release versions | Stable only | A release build is not shipped on pre-release libraries | `--include-prerelease` |
| Order in the run | First | The About dialog and the safety net then build on the current Enigma.Avalonia.Desktop | Last, beside the release |
| Tests | No new test; the whole suite is the proof | A version bump adds no behaviour | A version-pin test |
