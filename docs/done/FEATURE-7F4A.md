# FEATURE-7F4A — App icon in the output directory

## Summary

`Assets/app.ico` is now also copied, as `app.ico`, to the root of the build output and of the publish folder,
next to `Enigma.Launchpad.Desktop(.exe)`. Shortcuts, launchers and installers can point at a file. It is
still embedded as an `AvaloniaResource`, so the window icon and `avares://` lookups are unchanged. This
matches GitClient's FEATURE-5096.

## Files/modules touched

- Modified: `src/Enigma.Launchpad.Desktop/Enigma.Launchpad.Desktop.csproj`
- Modified: `tests/Enigma.Launchpad.Desktop.UnitTests/AppIconTests.cs`
- Modified: `docs/roadmap.md`, `docs/plan/FEATURE-7F4A.md`; created `docs/done/FEATURE-7F4A.md`

## Deviations & follow-ups

- None.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx -c Release`: 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx -c Release`: 969 total, 968 passed, 0 failed, 1 skipped
  (Unix-only). 1 test added: `TheIconFile_SitsInTheOutputDirectoryByteForByte` (the test project's output
  receives the referenced project's copy).
- `src/Enigma.Launchpad.Desktop/bin/Release/net10.0/app.ico` is `cmp`-identical to `Assets/app.ico`.
- `dotnet publish src/Enigma.Launchpad.Desktop -c Release -r win-x64 -o <scratchpad>`: the publish folder's
  `app.ico` is `cmp`-identical to `Assets/app.ico`.
- The embedded asset still resolves: the other `AppIconTests` read `avares://Enigma.Launchpad.Desktop/Assets/app.ico`.

## Documentation sweep

- Nothing stale: no document lists the output folder's contents.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| The item | `None Update` (the SDK already globs `Assets/app.ico` as `None`) | `Include` would duplicate the item |
