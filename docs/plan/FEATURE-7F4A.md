# FEATURE-7F4A — App icon in the output directory

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-7f4a-icon-in-output-dir`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Write the application icon to the build output directory, and therefore to the publish directory, next to
`Enigma.Launchpad.Desktop(.exe)`, as `app.ico`. Shortcuts, launchers and installers can then point at a file
instead of extracting the icon from the executable. This is part of the uniform baseline of every Enigma app
(GitClient's FEATURE-5096).

## Context & constraints

- `src/Enigma.Launchpad.Desktop/Assets/app.ico` is the `ApplicationIcon` and the window's `Icon`.
  `Assets/**` is an `AvaloniaResource` (embedded), so nothing lands in `bin/` today.
- Items with `CopyToOutputDirectory` are also published, and flow to a referencing project's output (the
  Desktop test project).
- `AppIconTests` (FEATURE-7298) pins the icon.

## Steps

1. `Enigma.Launchpad.Desktop.csproj`: copy `Assets/app.ico` to the output root as `app.ico`
   (`None Update`, `PreserveNewest`, `TargetPath="app.ico"`), with a one-line comment saying why. It stays
   an `AvaloniaResource` too.
2. `AppIconTests`: a test that `app.ico` sits in `AppContext.BaseDirectory`, byte for byte the embedded
   asset.
3. Verify: a Release build puts `app.ico` in `bin/Release/net10.0/`, and a `win-x64` publish puts it in
   `publish/`. Both are byte-identical to `Assets/app.ico`.

## Acceptance criteria

- `bin/<Configuration>/net10.0/app.ico` and the publish folder's `app.ico` exist, byte for byte
  `Assets/app.ico`.
- The embedded resource still resolves (`avares://…/Assets/app.ico` unchanged).
- The Release build has zero warnings; the whole suite is green.

## Out of scope

- The PNG copy (`app-256.png`) and the Linux hicolor icons (installed by `install.sh`).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Which file | `app.ico` only | "The app icon" is the `ApplicationIcon`; the narrowest reading, and GitClient's | Also `app-256.png` |
| Name and place | `app.ico` at the output root | One predictable name across every Enigma app | `Assets/app.ico` sub-folder; `Enigma.Launchpad.Desktop.ico` |
| Mechanism | `None Update` + `CopyToOutputDirectory` + `TargetPath` | Standard MSBuild; flows to publish with no custom target | A custom `Copy` target |
| Test | One Desktop test + a manual build/publish check | Pins the copy where the other icon copies are pinned | No test |
