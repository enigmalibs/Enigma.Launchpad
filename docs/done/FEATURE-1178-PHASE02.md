# FEATURE-1178-PHASE02 — URL tiles show website icons

## Summary

URL tiles now show their website's icon in place of the globe whenever PHASE01's fetcher finds one. Offline,
on a site with no icon, or on one offering only SVG, the tile keeps the globe.

- `IWebsiteIcons` / `WebsiteIcons` — the policy:
  - the address is re-parsed with `UrlNormalizer`; anything but http/https gives `null` without a request;
  - only the **site root** goes to the fetcher, rebuilt from scheme, IDN host (punycode) and port, so a
    shortcut's path, query, fragment, user name and password are never sent;
  - one fetch per site for the session; concurrent requests share it; it runs on the thread pool; a caller's
    token only ends its own wait;
  - bytes are decoded to a `Bitmap`, and an icon larger than 48 px is scaled down to fit (aspect kept);
  - anything thrown while fetching or decoding is logged at Warning and answered `null`.
- The tile mechanism from FEATURE-03CF is generalized, so programs and websites share it:
  `GetProgramIconAsync` → `GetTargetIconAsync`, `ProgramIcon` / `HasProgramIcon` → `TargetIcon` / `HasTargetIcon`.
  `UrlsPageViewModel` overrides the hook with `IWebsiteIcons` (new constructor dependency). Directory tiles ask
  for nothing.
- Tests never reach the network: `WindowHarness` registers an offline `IWebsiteIconFetcher` by default.

## Files

Created:
- `src/Enigma.Launchpad.Desktop/Services/WebsiteIcons.cs` — `IWebsiteIcons`, `WebsiteIcons`.
- `tests/Enigma.Launchpad.Desktop.UnitTests/WebsiteIconsTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/UrlTileIconTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/FakeWebsiteIconFetcher.cs`

Renamed:
- `tests/.../Infrastructure/FakeProgramIcons.cs` → `FakeTargetIcons.cs` — now implements `IProgramIcons` and
  `IWebsiteIcons`.

Modified:
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/CategoryPageViewModel.cs`, `ShortcutViewModel.cs`,
  `ApplicationsPageViewModel.cs` — the rename, with doc comments that now cover both kinds.
- `src/Enigma.Launchpad.Desktop/ViewModels/Pages/UrlsPageViewModel.cs` — `IWebsiteIcons` and the override.
- `src/Enigma.Launchpad.Desktop/Views/Pages/CategoryPageView.axaml` — bindings renamed.
- `src/Enigma.Launchpad.Desktop/DependencyInjection/ServiceCollectionExtensions.cs` — `IWebsiteIcons` registered.
- `tests/.../ApplicationTileIconTests.cs` — renamed members; the directory/link test is now
  `DirectoryAndLinkTiles_NeverAskForAProgramIcon`, since link tiles do ask for a website icon now.
- `tests/.../CategoryPageViewTests.cs`, `Infrastructure/Pages.cs` (`PageHarness.WebsiteIcons`),
  `Infrastructure/WindowHarness.cs` (offline fetcher), `CompositionRootTests.cs` (`IWebsiteIcons` resolves).
- `docs/done/FEATURE-1178-PHASE01.md` — correction: its line-endings note said the new files had CRLF in the
  working tree. They were LF all along; the check that reported CR had matched every line.

## Deviations & follow-ups

- No deviation from the plan.
- Fix budget: none used — the first build and test run were green.
- Visual check (throwaway test, deleted, never committed): the icons the PHASE01 live check downloaded from eight
  real sites (Avalonia docs, Wikipedia, GitHub, Microsoft Learn, Hacker News, Stack Overflow, Google, NuGet) were
  decoded through `WebsiteIcons.Decode` and drawn at 48 px. All eight render correctly; multi-size ICOs scale to
  48 px, and 32 px ones are kept.
- The test fake was renamed with `git mv`, which stages exactly that rename; everything else was staged by
  explicit path.
- Follow-ups (plan: out of scope): an opt-out setting; icons in the export/import tree.
- Documentation sweep: nothing changed here. The README says nothing about tile icons; PHASE03 documents the
  icon cache folder.
- Line endings: every touched file is LF, per `.gitattributes`.

## Decisions taken at build time

| Question                                  | Chosen                                                          | Why                                                                                   |
|-------------------------------------------|-----------------------------------------------------------------|---------------------------------------------------------------------------------------|
| Cache key                                 | The site root `Uri`                                             | Host case and default ports are already normalized; `http` and `https` stay apart     |
| Downscale rule                            | Fit within 48 px, aspect kept; smaller icons kept as they are   | Upscaling a 16 px favicon only blurs it twice                                         |
| Invalid input                             | `null` for anything `UrlNormalizer` refuses; `null` input throws | A shortcut can't hold a null target; a bad one is just "no icon"                      |
| One fake for both icon services           | `FakeTargetIcons` implements both interfaces                    | Same signature; `PageHarness` holds one instance per service, so tests tell them apart |
| Offline by default in `WindowHarness`     | Registered before the test's own replacements                   | A test can still script website icons when it needs them                              |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 542 passed, 0 failed, 0 skipped (518 before; 24 added: 19 in
  `WebsiteIconsTests`, 4 in `UrlTileIconTests`, 1 in `CompositionRootTests`). No test touches the network.
