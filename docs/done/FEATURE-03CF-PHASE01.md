# FEATURE-03CF-PHASE01 — Windows program icon service

## Summary

The desktop app can now read the icon Windows shows for a program, ready for PHASE02 to put on application tiles.
Nothing visible changes yet.

- `IProgramIconReader` / `ShellProgramIconReader` — the OS seam. On Windows it asks the shell
  (`SHCreateItemFromParsingName` → `IShellItemImageFactory.GetImage`, icon only) for a 48 × 48 image and copies it
  out with `GetDIBits` as top-down BGRA pixels with straight alpha (`ProgramIconPixels`). A failed HRESULT, or an
  image that is transparent everywhere, gives `null`; every COM object, bitmap and device context is released. On
  Linux and macOS it reads nothing.
- `IProgramIcons` / `ProgramIcons` — the policy, on every platform:
  - a network path (`\\…`, `//…`, `\\?\UNC\…`) is never read, so drawing a tile imported from someone else's
    file cannot make the app contact their host;
  - a path that is not an existing file is answered `null` without reaching the shell;
  - otherwise the reader runs on the thread pool and its pixels become an Avalonia `Bitmap` (`Bgra8888`,
    `AlphaFormat.Unpremul`);
  - every answer — icon or `null` — is kept per path for the session (case-insensitive on Windows); concurrent
    requests share one read; a caller's cancellation token only ends its own wait;
  - anything thrown while reading or converting is logged at Warning and answered `null`.
- Both are registered as singletons in `AddLaunchpadApp`.

## Files

Created:
- `src/Enigma.Launchpad.Desktop/Services/ProgramIconReader.cs` — `ProgramIconPixels`, `IProgramIconReader`,
  `ShellProgramIconReader` (Windows interop in a nested `[SupportedOSPlatform("windows")]` class).
- `src/Enigma.Launchpad.Desktop/Services/ProgramIcons.cs` — `IProgramIcons`, `ProgramIcons`.
- `tests/Enigma.Launchpad.Desktop.UnitTests/ProgramIconsTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/ShellProgramIconReaderTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/FakeProgramIconReader.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/RecordingLogger.cs`

Modified:
- `src/Enigma.Launchpad.Desktop/DependencyInjection/ServiceCollectionExtensions.cs` — two registrations.
- `tests/Enigma.Launchpad.Desktop.UnitTests/CompositionRootTests.cs` — `IProgramIcons` resolves, and is one
  shared instance.

## Deviations & follow-ups

- File layout: the plan listed `ProgramIconPixels.cs`, `IProgramIconReader.cs` and `IProgramIcons.cs` as separate
  files. They follow the house layout instead — an interface lives beside its implementation (`ClipboardService.cs`,
  `ProcessStarter.cs`) — giving two files: `ProgramIconReader.cs` and `ProgramIcons.cs`.
- Network-path rule extended to `\\?\UNC\…`, which starts with `\\` and so is covered by the same check.
- Fix budget: 2 of 3 cycles used. Cycle 1 fixed a test that compared thread ids after an `await` (the continuation
  can resume on the very pool thread that ran the read), and replaced it with a test where the caller blocks until
  the read has started. Cycle 2 passed the test's cancellation token to that wait (xUnit1051). No production code
  changed in either cycle.
- Follow-up: the cache keeps icons for the session; a program updated while the app runs shows its new icon on the
  next start (plan: out of scope).
- First native interop in the solution: `DllImport` + `[ComImport]`, relying on the existing
  `BuiltInComInteropSupport`. Trimming or Native AOT, if ever adopted, would need `GeneratedComInterface` instead.
- Documentation sweep: nothing changed. Nothing visible changes in this phase, and no README, CLAUDE.md or prose doc
  describes the services.
- Line endings: every touched file is LF, per `.gitattributes`.

## Decisions taken at build time

| Question                                   | Chosen                                                         | Why                                                                                       |
|--------------------------------------------|----------------------------------------------------------------|-------------------------------------------------------------------------------------------|
| Where the Windows-only code lives          | A private nested `NativeMethods` class marked for Windows      | One `OperatingSystem.IsWindows()` guard satisfies CA1416; the interop stays out of sight |
| Exceptions caught by `ProgramIcons`        | All, logged at Warning                                         | The read crosses COM, GDI and Skia; an icon is decoration and must never fault a tile     |
| Missing-file check                         | `File.Exists` on the pool thread, before the reader            | Keeps disk access off the UI thread and the shell out of the picture for a missing file   |
| Cached task for a network path             | A shared completed `null` task                                 | No thread-pool hop for an answer known up front                                           |
| Lock                                       | `private readonly object _sync`                                | Matches `LaunchpadSession` and `SettingsService`                                          |
| Disposing cached bitmaps                   | Not done                                                       | A few kilobytes per program, held for the app's lifetime, freed with the process          |
| Real-shell tests                           | `explorer.exe` (48 × 48, some opaque pixels); a missing path   | Always present on Windows; skipped elsewhere through `Assert.SkipUnless`                  |
| Channel order                              | Asserted through `Bitmap.CopyPixels` on the headless platform  | A BGRA/RGBA mix-up would tint every icon; the test catches it                             |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 454 passed, 0 failed, 0 skipped (434 before; 20 added: 14 in
  `ProgramIconsTests`, 4 in `ShellProgramIconReaderTests`, 2 in `CompositionRootTests`). The Windows-only
  reader tests ran on this machine. Two more runs with `--no-build` were also all green, checking the concurrency
  tests for flakiness.
