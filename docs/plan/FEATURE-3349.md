# FEATURE-3349 — Icons stored with shortcuts

**Status:** TODO
**Type:** FEATURE
**Branch:** `feature/feature-3349-phaseNN-<slug>` (one per phase)
**Run:** vibe/2026-10-13-icons-descriptions-tile-actions

## Objective

A shortcut's icon lives **in `launchpad.json`**, with the shortcut:

- an icon found automatically (a program's, a website's) is saved as soon as it is found and shown, so it is there
  at the next start without asking the shell or the network again;
- every form (applications, directories, URLs) has an **icon field** to pick an image or `.ico` file of one's own;
- the context menu of application and URL tiles has **Try to get icon**, which looks the icon up again — asking
  first when the current icon is one the user chose.

## Context & constraints

- Today `CategoryPageViewModel.GetTargetIconAsync` (virtual, `null`) is overridden by the applications page
  (`IProgramIcons`) and the URLs page (`IWebsiteIcons`); both services return a session-cached `IImage`.
  `ShortcutViewModel.TargetIcon` / `HasTargetIcon` load at creation and on a retarget, ignoring late answers.
- Program icons are never persisted: every start reads them from the shell again, so any transient shell failure
  shows up as an icon "lost after a restart" (see BUG-3079 for the reading itself).
- `launchpad.json` is strict: a missing required field makes the whole file unreadable (moved aside). Older builds
  skip unknown fields. The session coalesces saves; `Persist()` hands the page's whole category to it.
- Avalonia/Skia decodes PNG, ICO, GIF, JPEG, WebP and BMP and encodes PNG (`Bitmap.Save`); no SVG.
- ViewModel tests run without the Avalonia platform (`StubImage`); headless tests use `HeadlessAvaloniaFixture`.

## Design

### Model (PHASE01)

- Core `ShortcutIcon(string Png, bool IsCustom = false)`: `Png` is a PNG file in base64; `IsCustom` says the user
  chose it. `MaxBytes` (256 KB), `FromBytes(byte[], bool)`, `TryReadBytes(out byte[])` — valid base64, within
  `MaxBytes`, PNG signature.
- `Shortcut` gains `ShortcutIcon? Icon = null` (last). JSON: `"icon": { "png": "…", "isCustom": false }`; format
  version stays 2.
- Export v1 gains an optional `icon` per shortcut (written as stored); on import an icon that does not pass
  `TryReadBytes` is dropped and the shortcut kept.

### Found icons are kept (PHASE02)

- `IconPng` (Desktop, internal static): `Decode(bytes)` — any decodable image, scaled down to fit 48 px (moved out
  of `WebsiteIcons`); `Encode(bitmap)` — PNG; `Normalize(bytes)`.
- `IProgramIcons` / `IWebsiteIcons` return `Task<byte[]?>`: a normalized PNG. Their caching and rules are unchanged
  (network paths never read, only the site root fetched, the website disk cache kept).
- `IShortcutIcons` / `ShortcutIcons` — the one icon service pages use: `CanFind(kind)` (applications, URLs),
  `Show(ShortcutIcon)` → `IImage?` (null when undecodable), `FindAsync(kind, target)` → `IconImage?`
  (`IImage` + `ShortcutIcon` with `IsCustom = false`). Registered as a singleton.
- Pages take `IShortcutIcons` in the base constructor; the virtual hook and the per-page icon dependencies go.
- Tiles: a stored icon is shown at once, with no lookup. Without one (or with an automatic one that does not
  decode) and when the kind can find one, the tile looks it up; a found icon is shown **and stored** in the model,
  and the page saves — once after it finishes building its groups when found during that build, else right away,
  and only while the tile is still on the page.
- The form keeps the icon on save, except an automatic icon when the target changed (it is then found again).
- README "Your data": icons are kept in `launchpad.json`; the `icons` folder stays a cache of website icons.

### Icon field (PHASE03)

- `IPathPicker.PickImageFileAsync` (png, jpg, jpeg, gif, bmp, webp, ico).
- `IShortcutIcons.ReadFileAsync(path)` → `IconImage` (custom), off the UI thread; at most 10 MB; throws
  `InvalidDataException` with a user-worded reason for a file that is too large or not an image.
- Form: an **Icon** row — a preview plate (the icon, or the kind's glyph), **Choose…**, and **Use automatic icon**
  (applications, URLs) / **Remove** (directories) while the icon is a custom one; a hint saying where the icon comes
  from, and the reading error under it.

### Try to get icon (PHASE04)

- `IProgramIcons`, `IWebsiteIcons`, `IShortcutIcons.FindAsync` take `refresh`: the session cache is bypassed; a
  website is fetched again however fresh its disk entry (falling back to it when the fetch brings nothing).
- `ILaunchpadDialogs.ConfirmAsync` — a neutral yes/no question.
- Page: when the icon is custom, confirm "Replace your icon?"; look up with `refresh`; found → stored (automatic)
  and saved; nothing → a warning, icon unchanged. Pages take `IUserNotifications`.
- Tile: `FindIconCommand` / `CanFindIcon`; context menu **Try to get icon** for applications and URLs only.

## Phases

### PHASE01 — Icon in the shortcut model

**Status:** TODO

Steps:
1. `Shortcuts/ShortcutIcon.cs`; `Shortcut.Icon`.
2. `ShortcutExchange`: export and import of icons.
3. Tests: `ShortcutIcon` (round trip, invalid base64, oversized, non-PNG); store round trip (custom and automatic,
   none, older file without the field); exchange (round trip, invalid icon dropped and shortcut kept);
   `Duplicate` keeps icons.
4. README: export paragraph mentions icons.

Acceptance criteria:
- A shortcut with an icon is written and read back unchanged; a file without icons loads as before.
- An export carries icons; an imported invalid icon is dropped without losing the shortcut.
- 0 warnings; suite green.

### PHASE02 — Tiles keep found icons

**Status:** TODO

Steps:
1. `Services/IconPng.cs`; `ProgramIcons` / `WebsiteIcons` return normalized PNG bytes.
2. `Services/ShortcutIcons.cs` (`IShortcutIcons`, `IconImage`, `ShortcutIcons`); DI registration.
3. Pages: base constructor takes `IShortcutIcons`; hook removed; `ShowIcon`, `FindIconAsync`, `KeepFoundIcon`.
4. `ShortcutViewModel`: stored first, then found-and-kept; reload on icon or target change.
5. Editor keeps the icon, dropping an automatic one on a retarget.
6. Test doubles: `FakeShortcutIcons` replaces `FakeTargetIcons` in `PageHarness`; view tests adapted.
7. Tests: `IconPng` (encode/decode, scaled to 48, small kept); `ProgramIcons` / `WebsiteIcons` give PNG bytes;
   `ShortcutIcons` (show, undecodable → null, find per kind, directories never); tiles: stored icon without a
   lookup, found icon stored and saved, found-while-building saved once with every group, late answer for a tile no
   longer on the page ignored, retarget drops an automatic icon and keeps a custom one; restart: a found icon is
   shown from the file without a lookup.
8. README "Your data".

Acceptance criteria:
- A program or website icon shown once is in `launchpad.json` and shows at the next start with no shell read or
  network request.
- Tiles without a stored icon behave as before; directories never look one up.
- 0 warnings; suite green.

### PHASE03 — Icon field in the forms

**Status:** TODO

Steps:
1. `IPathPicker.PickImageFileAsync`.
2. `IShortcutIcons.ReadFileAsync`.
3. `ShortcutEditorViewModel`: icon state, preview, hint, error, `ChooseIconCommand`, `ResetIconCommand`;
   `LaunchpadDialogs` passes the service.
4. `ShortcutEditorView`: the icon row.
5. Tests: reading PNG, ICO and JPEG files (scaled to 48), too large, not an image; editor (existing icon previewed,
   choose → custom, error shown and icon unchanged, reset, a custom icon survives a retarget, `ToShortcut`); the
   tile shows a chosen icon after an edit.

Acceptance criteria:
- All three forms can set an image or `.ico` file as the icon; the tile shows it and it is saved.
- A custom icon can be removed again; a bad file shows a reason and changes nothing.
- 0 warnings; suite green.

### PHASE04 — Try to get icon

**Status:** TODO

Steps:
1. `refresh` through `IProgramIcons`, `IWebsiteIcons`, `IShortcutIcons`.
2. `ILaunchpadDialogs.ConfirmAsync`.
3. Page `FindShortcutIconAsync` + `IUserNotifications`; `ShortcutViewModel.FindIconCommand` / `CanFindIcon`.
4. Context menu item.
5. Tests: refresh bypasses the caches (program, website — including a fresh disk entry) and still falls back to the
   stored website icon; page flow (no question for an automatic icon, question for a custom one, declined →
   nothing, found → stored as automatic and saved, nothing → warning and unchanged); headless: the item on
   application and URL tiles, not on directory tiles.

Acceptance criteria:
- Try to get icon refreshes an application or URL tile's icon and saves it; it asks first over a custom icon.
- When nothing is found the user is told and the icon stays.
- 0 warnings; suite green.

## Out of scope

- SVG icons; icons in the export/import selection tree; pruning the website icon cache.
- Automatic icons for directories; periodic refresh of stored icons.
- Removing the website disk cache (still useful for new shortcuts to known sites).

## Decisions taken autonomously

| Question                         | Chosen                                                                       | Why                                                                                     | Alternatives rejected                                  |
|----------------------------------|------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------|--------------------------------------------------------|
| Where icons are serialized       | In `launchpad.json`, per shortcut, base64 PNG                                | What the draft asks; one file to back up, export and move                               | Separate image files referenced by path                |
| Stored size and format           | Normalized PNG, at most 48 px                                                | Tiles draw 24 px (sharp to 200 %); keeps each icon to a few KB of JSON                  | Original file bytes (unbounded size, any format)       |
| Custom vs automatic              | `isCustom` flag                                                              | "Try to get icon" must know whether to ask; a retarget must know what to drop           | An enum source field (two states only)                 |
| Automatic refresh                | Never automatic once stored; "Try to get icon" refreshes                     | Persisting is the fix for icons lost after a restart                                     | Weekly refresh of stored website icons                 |
| Retarget                         | Automatic icon dropped and found again; custom kept                          | The old program's icon would be wrong; a chosen image is the user's choice              | Keep every icon; drop every icon                       |
| Format version                   | Stays 2 (optional field)                                                     | Older builds skip unknown fields; nothing to migrate                                    | Version 3                                              |
| Exports                          | Carry icons as stored; invalid imported icon dropped, shortcut kept          | Moving shortcuts should not lose a chosen icon; an icon is decoration                   | Leave icons out; refuse the entry                      |
| Service shape                    | One `IShortcutIcons` for pages; sources return PNG bytes                     | Directories need decoding too; one place turns bytes into images                        | Keep per-page hooks returning images                   |
| When a found icon is saved       | Right away, or once after the page builds its groups                         | Saving mid-build would write a category missing its later groups                        | Dispatcher-posted saves (untestable without a platform) |
| Accepted files                   | png, jpg, jpeg, gif, bmp, webp, ico; ≤ 10 MB                                 | What Skia decodes; a cap keeps a stray huge file off the UI                             | SVG (new dependency)                                   |
| "Try to get icon" with nothing   | Warning notification, icon kept                                              | The user asked, so silence would read as a bug                                          | Silently keep; clear the icon                          |
| Directories                      | Custom icon only                                                             | No automatic source for folders                                                         | Shell folder icons                                     |
| Item shape                       | Four phases                                                                  | Model, persistence, form, menu: each one reviewable commit                              | One or two larger devs                                 |
