# FEATURE-3349-PHASE01 — Icon in the shortcut model

## Summary

A shortcut can now carry its icon, in `launchpad.json` and in exports. Nothing sets one yet; PHASE02 and PHASE03
do.

- `ShortcutIcon(string Png, bool IsCustom = false)` (Core):
  - `Png` is a PNG file in base64. Text keeps the document plain JSON, and makes two equal icons compare equal, so
    the page's "nothing changed" check after an edit still works.
  - `IsCustom` says the user chose it.
  - `FromBytes(byte[], bool)` builds one.
  - `TryReadBytes(out byte[])` accepts only valid base64 that decodes to at most `MaxBytes` (256 KB) and starts
    with the PNG signature. An over-long text is refused before it is decoded.
- `Shortcut` gains `Icon` (optional, last). In JSON: `"icon": { "png": "…", "isCustom": false }`. The format
  version stays 2, and files without icons load as before.
- Exports: each shortcut's icon is written as stored. On import, an icon that fails `TryReadBytes` — or has no
  image at all — is dropped and the shortcut kept. The icon's `png` is optional in the export reader, so a damaged
  icon costs only itself, not the whole file.
- The shortcut form keeps the edited shortcut's icon. Without this, an imported icon would be lost on the next
  edit until PHASE02.

## Files

Created:
- `src/Enigma.Launchpad.Core/Shortcuts/ShortcutIcon.cs`
- `tests/Enigma.Launchpad.Core.UnitTests/Shortcuts/ShortcutIconTests.cs`
- `tests/Enigma.Launchpad.Core.UnitTests/Infrastructure/SamplePng.cs` — a PNG header for tests that never decode.

Modified:
- `src/Enigma.Launchpad.Core/Shortcuts/Shortcut.cs` — `Icon`.
- `src/Enigma.Launchpad.Core/Exchange/ShortcutExchange.cs` — `ExportIcon`, write/read, remarks.
- `src/Enigma.Launchpad.Desktop/ViewModels/Dialogs/ShortcutEditorViewModel.cs` — keeps the icon.
- `README.md` — export section: icons held, example, what an import drops.
- Tests: `JsonLaunchpadStoreTests` (custom and automatic icons round trip, JSON shape, file without icons),
  `ShortcutExchangeTests` (icons exported and read back; invalid icons dropped), `ProfileTests` (duplicate keeps
  icons), `ShortcutEditorViewModelTests` (an edit keeps the icon).

## Deviations & follow-ups

- The plan didn't list the editor change. It was added so that an icon arriving through an import isn't dropped by
  an edit before PHASE02 lands.
- An export icon without `png` is dropped rather than making the file "damaged" (plan: "invalid icon dropped,
  shortcut kept").
- Fix budget: none used. The first build and test run were green.
- Documentation sweep: the README export section above. Nothing else became wrong.
- Line endings: every touched file is LF, per `.gitattributes`.

## Decisions taken at build time

| Question                        | Chosen                                                            | Why                                                                     |
|---------------------------------|-------------------------------------------------------------------|-------------------------------------------------------------------------|
| Where validation lives          | `ShortcutIcon.TryReadBytes` in Core (base64, size, PNG signature) | One rule for imports now and for decoding later; Core can't decode images |
| Over-long text                  | Refused by length before decoding                                 | An untrusted file can't make the reader allocate a huge buffer          |
| Store reading                   | Not validated on load                                             | A bad stored icon only costs its tile the glyph (PHASE02)               |
| Damaged export icon (`{}`)      | Dropped, file still read                                          | An icon is decoration                                                   |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 583 passed, 0 failed, 0 skipped (574 before; 9 added).
