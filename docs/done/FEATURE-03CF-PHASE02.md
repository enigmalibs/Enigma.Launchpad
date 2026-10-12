# FEATURE-03CF-PHASE02 — Tiles show program icons

## Summary

On the Applications page, a tile now shows its program's own icon — the one Explorer shows — in place of the
generic `AppWindow` glyph, whenever PHASE01's service can read it (Windows, an existing local file). Otherwise
the glyph stays: a missing program, a UNC path, and every tile on Linux and macOS look exactly as before.

- `CategoryPageViewModel.GetProgramIconAsync(target)` — a virtual hook answering `null`;
  `ApplicationsPageViewModel` overrides it with `IProgramIcons` (new constructor dependency). Directory and URL
  pages are unchanged and never ask.
- `ShortcutViewModel.ProgramIcon` (`IImage?`) and `HasProgramIcon`. The tile asks for its icon when it is created
  and again when an edit changes the target. Such an edit drops the old icon at once, and an icon that arrives for a
  target the tile no longer has is ignored. An edit that keeps the target doesn't ask again.
- Pages never wait: tiles appear with their glyph, and each icon replaces it as it arrives. A cached icon (a
  profile switch, a page shown again) is there from the first frame.
- The tile's 36 px plate holds both: the Phosphor glyph when `HasProgramIcon` is false, else an `Image` drawn at
  24 px with high-quality scaling from the 48 px source. The tile's automation name is still the shortcut's name;
  the image is decorative.

## Files

Modified:
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/CategoryPageViewModel.cs` — the hook.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/ApplicationsPageViewModel.cs` — `IProgramIcons` and the override.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/ShortcutViewModel.cs` — `ProgramIcon`, `HasProgramIcon`, loading.
- `src/Enigma.Launchpad.Desktop/Views/Pages/CategoryPageView.axaml` — image and glyph in the tile plate.
- `src/Enigma.Launchpad.Desktop/Themes/Styles.axaml` — `Image.tileIcon`.
- `tests/Enigma.Launchpad.Desktop.UnitTests/CategoryPageViewTests.cs` — headless test: glyph first, then the
  program's icon once released; a program without one keeps its glyph.
- `tests/Enigma.Launchpad.Desktop.UnitTests/CompositionRootTests.cs` — `BuildProvider` takes an optional
  `replace` callback that registers test doubles.
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/WindowHarness.cs` — passes `replace` through.
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/Pages.cs` — `PageHarness.ProgramIcons`.

Created:
- `tests/Enigma.Launchpad.Desktop.UnitTests/ApplicationTileIconTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/FakeProgramIcons.cs` — `FakeProgramIcons` (scripted
  or held answers) and `StubImage`.

## Deviations & follow-ups

- High-quality scaling is set on the `Image` element, not in `Styles.axaml` as the plan had it. In Avalonia 12,
  `RenderOptions.BitmapInterpolationMode` is set through static `Set…`/`Get…` methods, not an `AvaloniaProperty`, so
  a style `Setter` cannot target it (`AVLN2000`).
- Fix budget: 1 of 3 cycles used — the style setter above. The first run after that fix was green.
- Visual check: a throwaway test (deleted, never committed) saved the real shell icons of `explorer.exe`,
  `notepad.exe` and `cmd.exe` as PNGs through `ShellProgramIconReader` → `ProgramIcons.ToBitmap`. Colours and
  transparency were correct, so the channels aren't swapped and straight alpha is honoured.
- Not checked on a real desktop session: the running app wasn't launched. The headless view test covers the
  binding, and the probe covers the pixels.
- Follow-ups (plan: out of scope): icons in the export/import selection tree (its application lines still use the
  glyph); Linux and macOS icon sources; refreshing an icon whose program changed while the app runs.
- Documentation sweep: nothing changed. The README describes tiles without mentioning their icon, so nothing in it
  became wrong.
- Line endings: every touched file is LF, per `.gitattributes`.

## Decisions taken at build time

| Question                                  | Chosen                                                            | Why                                                                                     |
|-------------------------------------------|-------------------------------------------------------------------|-----------------------------------------------------------------------------------------|
| Where loading starts                      | The tile's constructor and `Update`, as a discarded task          | The one place every tile passes through (load, add, profile switch); the task never faults |
| What decides "the target changed"         | Ordinal string comparison of the targets                          | Same rule the cache would apply on Linux; on Windows a case-only edit re-asks the cache, which answers at once |
| Old icon during a retarget                | Cleared immediately                                               | Briefly showing the glyph is honest; showing the previous program's icon is not         |
| Glyph and image together                  | Both in a `Panel`, visibility bound to `HasProgramIcon`           | No converter, no template switch; the plate keeps its size either way                   |
| Interpolation mode                        | Attribute on the `Image` element                                  | The only way Avalonia 12 accepts it (see deviations)                                    |
| Replacing services in headless tests      | Optional `replace` callback on `BuildProvider` / `WindowHarness`  | Uses the real composition root and swaps only `IProgramIcons`, so the view test doesn't depend on the OS |
| ViewModel tests' image                    | `StubImage`, an `IImage` drawing nothing                          | ViewModel tests run without the Avalonia platform                                       |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental` — 0 warnings, 0 errors (no `AVLN` XAML diagnostics).
- `dotnet test --solution Enigma.Launchpad.slnx` — 464 passed, 0 failed, 0 skipped (454 before; 10 added: 9 in
  `ApplicationTileIconTests`, 1 in `CategoryPageViewTests`). Two more `--no-build` runs were also all green.
