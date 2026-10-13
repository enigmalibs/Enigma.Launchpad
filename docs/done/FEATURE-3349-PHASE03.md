# FEATURE-3349-PHASE03 — Icon field in the forms

## Summary

The application, folder and link forms now have an **Icon** row, so any shortcut can use an image of the user's
own — PNG, JPEG, GIF, BMP, WebP or ICO.

- `IPathPicker.PickImageFileAsync(title)` uses an "Images" filter (`*.png *.jpg *.jpeg *.gif *.bmp *.webp *.ico`)
  plus All files.
- `IShortcutIcons.ReadFileAsync(path)` runs on the thread pool and returns an `IconImage` marked `IsCustom`. The
  image is normalized like every icon: a PNG of at most 48 px.
  - A file over `MaxFileBytes` (10 MB) is refused before it is read.
  - Bytes the decoder rejects throw `InvalidDataException` with a user-worded message.
  - I/O and access errors pass through.
- `ShortcutEditorViewModel` (now also takes `IShortcutIcons`) exposes:
  - `Icon` — what will be saved;
  - `IconPreview` / `HasIconPreview`, and `KindGlyph` for the empty plate;
  - `IconHint` — where the icon comes from;
  - `IconError`;
  - `CanResetIcon` and `ResetIconLabel` ("Use automatic icon" for applications and links, "Remove" for folders);
  - `ChooseIconCommand` and `ResetIconCommand`.
- An icon found from the target is set aside while the target, as it would be saved, differs from the original.
  The preview goes back to the glyph, and returns if the original target is typed back. A chosen image is never
  set aside. `ToShortcut` saves `Icon`, which replaces PHASE02's inline retarget rule with the same behaviour.
- A picked file that can't be used shows its reason under the row and leaves the icon as it was. The form stays
  valid.
- View: a 36 px plate (the image, or the kind's glyph), the hint, **Choose…**, and the reset button when the icon is
  a chosen image.

## Files

Modified:
- `src/Enigma.Launchpad.Desktop/Services/PathPicker.cs` — `PickImageFileAsync`, image filter.
- `src/Enigma.Launchpad.Desktop/Services/ShortcutIcons.cs` — `ReadFileAsync`, `MaxFileBytes`.
- `src/Enigma.Launchpad.Desktop/ViewModels/Dialogs/ShortcutEditorViewModel.cs` — the icon field.
- `src/Enigma.Launchpad.Desktop/Views/Dialogs/ShortcutEditorView.axaml` — the icon row.
- `src/Enigma.Launchpad.Desktop/Services/LaunchpadDialogs.cs` — passes `IShortcutIcons` to the form.
- `README.md` — "Your data": a chosen image is kept too.
- Tests:
  - `ShortcutIconsTests` — PNG, JPEG and ICO files read and scaled; not an image; over 10 MB; missing file.
  - `ShortcutEditorViewModelTests` — glyph and hints, kept icon shown, choose, cancel, failures, reset, set aside
    and back.
  - `ApplicationTileIconTests` — a chosen image is shown and saved; giving it up finds the program's icon.
  - `CategoryPageViewTests` — the form's icon row (headless).
  - `Infrastructure/FakePathPicker.cs`, `FakeShortcutIcons.cs` (`ReadFileAsync`), `TestImages.cs` (JPEG, ICO).

## Deviations & follow-ups

- `PickImageFileAsync` takes no start path (the plan's sketch had none either). There's nothing meaningful to start
  from: the target is a program or a URL, not an image folder.
- A folder shortcut can only carry an automatic icon if it came in with one (an edited export). Its hint then reads
  "The icon it came with." rather than naming a program or site.
- Fix budget: none used. The first build and test run were green.
- Documentation sweep: README "Your data" mentions chosen images. Nothing else became wrong.
- Line endings: every touched file is LF, per `.gitattributes`.

## Decisions taken at build time

| Question                                  | Chosen                                                          | Why                                                                                |
|-------------------------------------------|-----------------------------------------------------------------|------------------------------------------------------------------------------------|
| Where the icon row sits                   | Last, after the description                                     | The fields that define the shortcut come first; the icon is decoration            |
| Automatic icon while the target changes   | Set aside live in the preview, restored for the original target | The form shows exactly what will be saved                                          |
| A file that can't be used                 | Reason under the row; icon and validity unchanged               | A failed pick isn't a broken form                                                  |
| Size cap                                  | 10 MB, checked before reading                                   | No icon is that large; a stray photo or video never reaches the decoder            |
| Decoder failures                          | Wrapped in `InvalidDataException` with one message              | The decoder's exceptions aren't a documented set; to the user they mean one thing  |
| Reset label                               | "Use automatic icon" / "Remove"                                 | Says what happens: a found icon comes back, or a folder goes back to its glyph     |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 630 passed, 0 failed, 0 skipped (610 before; 20 added).
