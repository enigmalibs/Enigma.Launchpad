# FEATURE-424E — Update NuGet packages

## Summary

Every package the solution references is now at its latest stable version on nuget.org:

- Microsoft.Extensions.DependencyInjection(.Abstractions), Logging.Abstractions, Hosting: already at 10.0.14,
  since 1.3.0 (FEATURE-EB8F);
- the coupled Avalonia set (Avalonia, Desktop, Themes.Fluent, Fonts.Inter, Headless, Skia): 12.1.1 → 12.1.4;
- Enigma.Avalonia.Desktop: 1.2.0 → 1.3.0 (its nuspec pins Avalonia 12.1.4; no API or theme-key change).

The Avalonia group's comment now names the 1.3.0 / 12.1.4 pairing.

## Files/modules touched

- Modified: `Directory.Packages.props`
- Modified: `docs/roadmap.md`, `docs/plan/FEATURE-424E.md`
- Created: `docs/done/FEATURE-424E.md`

## Deviations & follow-ups

- None. No code change was needed.
- Built on 1.2.0, then merged on top of 1.3.0 (FEATURE-EB8F), which had already moved Microsoft.Extensions to
  10.0.14. This dev's diff is the Avalonia set and Enigma.Avalonia.Desktop.
- The roadmap diff is wide: the Status column grew to fit `IN PROGRESS`, and `roadmap.py` re-padded every row.
  Ignoring whitespace, only the status cells changed.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx -c Release --no-incremental`: 0 warnings, 0 errors.
- `dotnet build Enigma.Launchpad.slnx --no-incremental` (Debug): 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx -c Release`: 965 total, 964 passed, 0 failed, 1 skipped (the
  Unix-only resolver test on this Windows host). Debug: the same.
- `dotnet list Enigma.Launchpad.slnx package --outdated`: "has no updates" for all four projects.
- No test added: a version bump adds no behaviour.

## Documentation sweep

- Nothing stale: the README names Avalonia 12 and Enigma.Avalonia.Desktop without a version.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Pre-release versions | Not considered | `--outdated` lists stable versions by default; a release is not shipped on pre-releases |
| `CommunityToolkit.Mvvm` | Held at 8.4.2 | Already latest, and what Enigma.Avalonia.Desktop 1.3.0 pins |
