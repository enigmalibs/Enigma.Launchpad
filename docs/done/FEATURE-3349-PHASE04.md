# FEATURE-3349-PHASE04 — Try to get icon

## Summary

Application and link tiles have a new context-menu item, **Try to get icon**. It looks the icon up again — from the
program, or from the website itself — and saves what it finds.

- Sources: `IProgramIcons.RefreshAsync` / `IWebsiteIcons.RefreshAsync` read again instead of answering the session
  cache. A read already underway is shared, not started twice.
  - For a website, the site is asked even when its disk entry is fresh. The answer is stored. When the site gives
    nothing, the stored icon is still the answer.
  - The existing rules hold: network paths are never read, and only the site root is fetched.
- `IShortcutIcons.RefreshAsync(kind, target)` mirrors `FindAsync` over the refresh methods. A folder still finds
  nothing.
- `ILaunchpadDialogs.ConfirmAsync(title, message, confirmText)`: a neutral question (not red), with Cancel as the
  default.
- `CategoryPageViewModel.FindShortcutIconAsync` (pages now also take `IUserNotifications`):
  1. When the icon is an image the user chose, ask "Replace your icon?" ("“Code” uses an image you chose. Look for
     the program's own icon and use it instead?" / Replace). Declining does nothing.
  2. Look up with refresh.
  3. If the tile was edited, moved off the page or deleted meanwhile, ignore the answer.
  4. If nothing was found, show the warning "No icon found — No icon could be found for “Code”. Its icon is
     unchanged."
  5. Otherwise the tile shows the found icon, the shortcut keeps it as automatic, and the page saves.
- `ShortcutViewModel.FindIconCommand` / `CanFindIcon` (applications and links). `SetFoundIcon` is now shared by
  the automatic lookup and this command.
- Context menu: **Try to get icon** (`ArrowsClockwise`) after **Edit…**, visible only where `CanFindIcon`.

## Files

Created:
- `tests/Enigma.Launchpad.Desktop.UnitTests/TryToGetIconTests.cs`

Modified:
- `src/Enigma.Launchpad.Desktop/Services/ProgramIcons.cs`, `WebsiteIcons.cs`, `ShortcutIcons.cs` — refresh.
- `src/Enigma.Launchpad.Desktop/Services/LaunchpadDialogs.cs` — `ConfirmAsync`.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/CategoryPageViewModel.cs` — notifications,
  `FindShortcutIconAsync`.
- `ApplicationsPageViewModel.cs`, `UrlsPageViewModel.cs`, `DirectoriesPageViewModel.cs` — constructors.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/ShortcutViewModel.cs` — `FindIconCommand`, `CanFindIcon`,
  `SetFoundIcon`.
- `src/Enigma.Launchpad.Desktop/Views/Pages/CategoryPageView.axaml` — the menu item.
- `README.md` — the tile actions sentence mentions the item.
- Tests:
  - `ProgramIconsTests` — refresh reads again, shares a read underway, never reads a network path.
  - `WebsiteIconsTests` — refresh asks over a fresh entry and stores the answer, falls back to the stored icon,
    fetches nothing for a non-web address.
  - `ShortcutIconsTests` — refresh goes through the sources.
  - Doubles: `FakeTargetIcons`, `FakeShortcutIcons`, `ScriptedDialogs`, `Pages`.

## Deviations & follow-ups

- The plan sketched a `refresh` parameter. The sources and the service expose separate `RefreshAsync` methods
  instead: a boolean flag argument reads poorly at call sites, and inserting one before the `CancellationToken`
  would have broken every positional call.
- The menu item's visibility is checked through `CanFindIcon` on the ViewModel, not by opening the context menu
  headlessly. The binding is compiled, so a wrong path fails the build.
- Fix budget: none used. The first build and test run were green.
- Documentation sweep: README tile actions sentence. Nothing else became wrong.
- Line endings: every touched file is LF, per `.gitattributes`.

## Decisions taken at build time

| Question                                   | Chosen                                                     | Why                                                                        |
|--------------------------------------------|------------------------------------------------------------|----------------------------------------------------------------------------|
| API shape                                  | `RefreshAsync` beside `GetAsync` / `FindAsync`             | Clear call sites; no boolean flag                                          |
| Refresh while a lookup is underway         | Shared                                                     | The lookup underway is already fresh                                       |
| Confirmation style                         | Neutral question, Cancel by default                        | Replacing a chosen image is reversible but unwanted by accident            |
| Answer for a tile changed meanwhile        | Ignored, no notification                                   | The user moved on; a late icon or warning would be confusing               |
| Success feedback                           | The tile's icon changes; no notification                   | Visible on its own                                                         |
| Folder tiles                               | No menu item; the command cannot execute                   | A folder has no icon to find                                               |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 647 passed, 0 failed, 0 skipped (630 before; 17 added).
