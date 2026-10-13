# BUG-3079-PHASE02 — Website icon finding

## Summary

More sites now give their icon: many declare their raster icons only in a web app manifest, and some servers
answered the old bare `Accept: text/html` badly.

- `FaviconLinks`:
  - `FindLinked(html, page)` returns the page's icon links with their sizes, in page order. `Find` is now
    `Rank(FindLinked(...))`, with unchanged behaviour.
  - `FindManifest(html, page)` returns the first `<link rel="manifest">` over http or https.
  - `ReadManifest(json, manifest)` reads the manifest's `icons`:
    - `src` resolves against the manifest's final address;
    - `sizes` counts as the largest declared; `any`, or no readable size, counts as 192;
    - SVG, by `type` or extension, is skipped, as are non-http(s) sources and monochrome-only `purpose`;
    - malformed JSON or entries are ignored.
  - `Rank(icons)` is the existing rule — the smallest at least 48 px first, then the largest below, with the given
    order breaking ties — applied to page links and manifest icons together.
- `WebsiteIconFetcher`:
  - The page request now sends `Accept: text/html,application/xhtml+xml;q=0.9,*/*;q=0.8` (`PageAccept`).
  - When the page links a manifest, it is fetched: at most 64 KB (`MaxManifestBytes`), success status only, the
    same timeouts, cookies and redirects. Its icons join the page's links before ranking.
  - Still at most three declared icons, then `/favicon.ico`. A site is asked at most 6 times: page, manifest, three
    icons, `/favicon.ico`.
  - A manifest that can't be had or read only means fewer candidates (Debug log).
- README "Your data": what a site is asked now includes the manifest it links.

## Files

Modified:
- `src/Enigma.Launchpad.Desktop/Services/FaviconLinks.cs` — linked icons with sizes, manifest link, manifest
  icons, shared ranking, shared address and size helpers.
- `src/Enigma.Launchpad.Desktop/Services/WebsiteIconFetcher.cs` — manifest fetch, merged candidates, `PageAccept`,
  `MaxManifestBytes`, remarks.
- `README.md` — "Your data".
- Tests:
  - `FaviconLinksTests` — manifest link found (relative, case-insensitive, first wins) or not; manifest icons with
    sizes, `any`, cross-host; nine manifests with no usable icon; joint ranking.
  - `WebsiteIconFetcherTests` — a manifest-only icon is downloaded; a missing manifest leaves `/favicon.ico`; an
    oversized manifest isn't read; the 6-request bound; the new `Accept`.

## Deviations & follow-ups

- The plan's acceptance criterion counted "1 page + 1 manifest + 4 icon requests". That is what was built: three
  declared icons plus `/favicon.ico` are the four icon requests.
- A manifest icon of unreadable size counts as 192 px. The plan said "counted as unsized", but manifest icons are
  app icons, and 192 is the common default. This is a separate constant from the link defaults.
- Fix budget: none used. The first build and test run were green.
- No test touches the network; all go through `StubHttpHandler`.
- Documentation sweep: README "Your data", as above.
- Line endings: every touched file is LF, per `.gitattributes`.

## Decisions taken at build time

| Question                           | Chosen                                                      | Why                                                                     |
|------------------------------------|-------------------------------------------------------------|-------------------------------------------------------------------------|
| Which manifest                     | The first `rel="manifest"` link                             | A page has one; a second would be a mistake                             |
| Manifest size cap                  | 64 KB                                                       | Real manifests are a few KB; bounds memory like the page and icon caps  |
| Unsized manifest icon              | 192 px                                                      | App icons are large; keeps them behind a sized ≥ 48 px link             |
| `purpose`                          | Skip monochrome-only; keep `maskable` and `any`             | A monochrome silhouette would show as a block                           |
| Order before ranking               | Page links, then manifest icons                             | Ties go to what the page itself declares                                |
| `Accept` for the manifest          | `application/manifest+json`, then JSON, then anything       | Honest about what is wanted, tolerant of mislabelled servers            |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 675 passed, 0 failed, 0 skipped (654 before; 21 added).
