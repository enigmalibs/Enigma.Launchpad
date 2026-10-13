# FEATURE-3349-PHASE02 — Tiles keep found icons

## Summary

A program's or website's icon, once found and shown, is now saved with its shortcut in `launchpad.json`. At the
next start the tile shows it straight away, without reading the shell or asking the site. This is the fix for
icons "lost after a restart".

- `IconPng` (internal static) is the one place images are normalized:
  - `Decode` takes any image Avalonia decodes, scaled down to fit `MaxSize` (48 px) — moved out of `WebsiteIcons`.
  - `Encode` writes a PNG.
  - `Normalize` does both.
- `IProgramIcons` / `IWebsiteIcons` now answer `Task<byte[]?>`: a PNG no larger than 48 px. Their rules are
  unchanged: session cache, network paths never read, only the site root fetched, 7-day disk cache with stale
  fallback. The website disk cache still stores the file as served.
- `IShortcutIcons` / `ShortcutIcons` (singleton) is the one icon service the pages use:
  - `CanFind(kind)` is true for applications and URLs.
  - `Show(ShortcutIcon)` returns `IImage?`, or `null` when the icon isn't a valid PNG or doesn't decode (Debug log,
    never throws).
  - `FindAsync(kind, target)` returns an `IconImage` — an image plus a `ShortcutIcon` with `IsCustom = false`.
- Pages: the base constructor takes `IShortcutIcons`. The virtual `GetTargetIconAsync` hook and the per-page
  `IProgramIcons` / `IWebsiteIcons` dependencies are gone, and Directories now gets the service to show chosen
  icons.
- Tiles (`ShortcutViewModel`):
  1. A kept icon is shown at once, with no lookup.
  2. Otherwise — no icon, or an automatic one that doesn't decode — and only for a kind that can find one, the
     icon is looked up.
  3. A found icon is shown, taken into the model, and handed to `CategoryPageViewModel.KeepFoundIcon`.
  - A chosen (`IsCustom`) icon is never replaced, even when it doesn't decode: the tile shows the glyph.
  - Late answers are ignored when the target changed, or the shortcut got another icon meanwhile.
  - `Update` reloads when the target *or* the icon changes.
- `KeepFoundIcon` saves right away while the tile is on the page. During `ReadGroups` the category isn't complete,
  and an answer arriving at once (session cache) would otherwise save a category missing its later groups; such
  icons are saved once, after the build. A tile no longer on the page saves nothing.
- The form keeps the edited shortcut's icon, except an automatic one when the target changed. That one is dropped,
  and the tile finds the new target's icon.

## Files

Created:
- `src/Enigma.Launchpad.Desktop/Services/IconPng.cs`
- `src/Enigma.Launchpad.Desktop/Services/ShortcutIcons.cs` — `IconImage`, `IShortcutIcons`, `ShortcutIcons`.
- `tests/Enigma.Launchpad.Desktop.UnitTests/IconPngTests.cs`, `ShortcutIconsTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/FakeShortcutIcons.cs`, `TestImages.cs`

Modified:
- `src/Enigma.Launchpad.Desktop/Services/ProgramIcons.cs`, `WebsiteIcons.cs` — PNG bytes.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/CategoryPageViewModel.cs` — icon service, `ShowIcon`,
  `FindIconAsync`, `CanFindIcons`, `KeepFoundIcon`, build guard.
- `ApplicationsPageViewModel.cs`, `UrlsPageViewModel.cs`, `DirectoriesPageViewModel.cs` — constructors.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/ShortcutViewModel.cs` — kept-then-found loading.
- `src/Enigma.Launchpad.Desktop/ViewModels/Dialogs/ShortcutEditorViewModel.cs` — retarget drops an automatic icon.
- `src/Enigma.Launchpad.Desktop/DependencyInjection/ServiceCollectionExtensions.cs` — registration.
- `README.md` — "Your data": icons are kept in `launchpad.json`; the `icons` folder is a website-icon cache.
- Tests:
  - `ApplicationTileIconTests`, `UrlTileIconTests` — rewritten on `FakeShortcutIcons`, plus the persistence cases.
  - `ProgramIconsTests`, `WebsiteIconsTests` — bytes.
  - `CategoryPageViewTests` — the headless icon test decodes a real PNG.
  - `CompositionRootTests` — the service resolves.
  - `ShortcutEditorViewModelTests` — the retarget rule.
  - `Infrastructure/FakeTargetIcons.cs` — bytes; `Infrastructure/Pages.cs` — `PageHarness.Icons`.
  - `Infrastructure/HeadlessAvaloniaFixture.cs` — see below.

## Deviations & follow-ups

- `HeadlessAvaloniaFixture.Run<TResult>` recursed into itself: an expression-bodied lambda returning the result
  re-selects the same overload, so the stack overflowed. No test had called it before; a new `IconPngTests` test
  did, and it crashed the test process. Fixed with a block-bodied lambda. This was fix cycle 1 of 3; the next run
  was green.
- `ProgramIcons.IconSize` is now `IconPng.MaxSize`. `WebsiteIcons.IconSize` and `WebsiteIcons.Decode` were removed
  (moved to `IconPng`).
- Bitmaps shown on tiles are no longer shared through the sources' session caches: each tile decodes its own 48 px
  PNG (≈ 9 KB) when it is created.
- Follow-up: `ShortcutIcons.Show` decodes on the UI thread. That is negligible for 48 px PNGs. A page with
  thousands of tiles might want an off-thread decode.
- Documentation sweep: README "Your data", as above.
- Line endings: every touched file is LF, per `.gitattributes`.

## Decisions taken at build time

| Question                                      | Chosen                                                              | Why                                                                                 |
|-----------------------------------------------|---------------------------------------------------------------------|-------------------------------------------------------------------------------------|
| Where found icons become images               | `ShortcutIcons` only; sources answer PNG bytes                      | One decode path for kept and found icons; the bytes are what gets stored            |
| Stored automatic icon that doesn't decode     | Looked up again and replaced                                        | It was found automatically, so finding it again is the repair                       |
| Stored custom icon that doesn't decode        | Glyph, never replaced                                               | The user's choice is not overwritten behind their back                              |
| Save during the page's build                  | Deferred to one save after the build                                | A save mid-build would write the category without its later groups                  |
| Tile no longer on the page                    | Nothing saved                                                       | Its profile isn't in view; the icon is found again when it is                       |
| Test double for pages                         | `FakeShortcutIcons` (no platform), `FakeTargetIcons` kept for the sources | ViewModel tests stay platform-free; service tests use real PNGs headlessly    |
| Retarget rule location                        | `ShortcutEditorViewModel.ToShortcut`                                | The form decides what is saved; PHASE03's icon field builds on it                   |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 610 passed, 0 failed, 0 skipped (583 before). New:
  `IconPngTests` (4), `ShortcutIconsTests` (10), persistence and kept-icon cases in `ApplicationTileIconTests`, and
  three retarget-rule editor tests. Directory-specific tile cases moved from `UrlTileIconTests` into
  `ApplicationTileIconTests`.
