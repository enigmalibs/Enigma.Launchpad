# BUG-3079-PHASE01 — Program icon reading

## Summary

Program icons are read more reliably on Windows.

- **STA threads.** Each read by `ShellProgramIconReader` now runs on a new background thread in a single-threaded
  apartment, and the caller waits for it.
  - Before, reads ran on thread-pool threads (multithreaded apartment), many at once at start-up. The shell and
    the icon handlers it loads expect an STA, and some fail intermittently outside one. That explains an icon
    present in one session and gone in the next.
  - `OnStaThread` carries an exception back to the caller with its original stack (`ExceptionDispatchInfo`).
    `ProgramIcons` still logs it and falls back to the glyph.
- **Classic-icon fallback.** When `IShellItemImageFactory.GetImage` fails, or gives an image with no alpha at all,
  the reader now asks `SHGetFileInfo(SHGFI_ICON | SHGFI_LARGEICON)` for the file's classic icon.
  - Before, an icon with no alpha was discarded and the tile kept its glyph, although the program has an icon.
  - The fallback reads the colour bitmap (`GetIconInfo` + `GetDIBits`, 32 bpp). When the colours carry no alpha,
    transparency comes from the icon's mask: black is opaque, white is clear.
  - Every handle is released (`DestroyIcon`, `DeleteObject` on both bitmaps).
  - The fallback is the shell's large icon size (32 px at 96 DPI). `ProgramIcons` normalizes it like any icon, so
    it is drawn smaller-sourced but correct.
- `HasAlpha` and `ApplyMask` are internal, pure, and unit-tested.

## Files

Modified:
- `src/Enigma.Launchpad.Desktop/Services/ProgramIconReader.cs` — STA runner, classic-icon fallback, mask
  transparency, P/Invoke declarations, remarks.
- `tests/Enigma.Launchpad.Desktop.UnitTests/ShellProgramIconReaderTests.cs` — eight thread-pool reads at once,
  classic icon alone, missing file on both paths, STA apartment, exception carried back, alpha detection, mask
  application, mismatched mask refused.

## Deviations & follow-ups

- Fix budget: 2 of 3 cycles used.
  - Cycle 1: platform analyzer CA1416 refused calls to Windows-only members from inside a lambda in `Read`. They
    moved into a `[SupportedOSPlatform("windows")]` method.
  - Cycle 2: the same rule in the tests, where `Assert.SkipUnless(OperatingSystem.IsWindows())` isn't a recognized
    guard. The Windows-only tests now carry `[SupportedOSPlatform("windows")]` too.
  - The next run was green.
- Not reproduced: I couldn't provoke a real STA-sensitive handler failure in a test. The STA change is verified by
  checking the apartment the work runs in, and by eight concurrent thread-pool reads.
- Not checked on a real desktop session: the app was not launched. The real-shell tests read `explorer.exe` through
  both paths.
- Follow-up (plan: out of scope): an extra-large classic icon (`SHGetImageList(SHIL_EXTRALARGE)`) would make the
  fallback 48 px.
- Documentation sweep: nothing user-facing changed wording; nothing became wrong.
- Line endings: every touched file is LF, per `.gitattributes`.

## Decisions taken at build time

| Question                              | Chosen                                                    | Why                                                                         |
|---------------------------------------|-----------------------------------------------------------|-----------------------------------------------------------------------------|
| Thread per read vs a shared STA queue | A thread per read                                         | Simple, isolated (a hung handler can't block others), cheap at this volume  |
| Exceptions on the STA thread          | Captured and rethrown on the caller                       | `ProgramIcons` keeps one place deciding what a failure means                |
| When to fall back                     | `GetImage` failed, or its image has no alpha anywhere     | The two observed ways an existing icon went missing                         |
| Monochrome icons                      | No icon (glyph)                                           | Nothing worth showing; they're vanishingly rare for programs                |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 654 passed, 0 failed, 0 skipped (647 before; 7 added). The
  Windows shell tests ran, not skipped, on Windows 11.
