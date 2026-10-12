# FEATURE-1178 — Website icons on URL tiles

**Status:** IN PROGRESS
**Type:** FEATURE
**Branch:** `feature/feature-1178-phaseNN-<slug>` (one per phase)
**Run:** vibe/2026-10-12-url-favicons

## Objective

On the **URLs** page, each tile shows the website's own icon — its favicon — instead of the generic `Globe`
glyph, whenever one can be fetched. When none can (offline, the site has no icon, or only an SVG one), the tile
keeps the glyph. This mirrors what FEATURE-03CF did for application tiles.

## Context & constraints

- FEATURE-03CF added `IProgramIcons` and a tile hook: `CategoryPageViewModel.GetProgramIconAsync(target)`
  (answers `null`), overridden by `ApplicationsPageViewModel`; `ShortcutViewModel.ProgramIcon` / `HasProgramIcon`
  load on creation and on a target change, ignoring late answers; the tile plate shows the image or the glyph.
- URL targets are http/https only (`UrlNormalizer.TryParse`) and may come from import files.
- The app makes no network request today. `Microsoft.Extensions.Http` is not referenced. `TimeProvider` and
  `IAppPaths` are registered by `AddLaunchpadCore`; `AtomicFile` writes text atomically.
- Avalonia's `Bitmap` (Skia) decodes PNG, ICO, GIF, JPEG, WebP and BMP; it does not decode SVG.

## Design

### Fetching (PHASE01)

- `IWebsiteIconFetcher` — `Task<byte[]?> FetchAsync(Uri site, CancellationToken)`: the bytes of the site's best
  icon, or `null`.
- `WebsiteIconFetcher` — owns one long-lived `HttpClient` over a `SocketsHttpHandler` (no cookies, ≤ 5 redirects,
  automatic decompression, 5 s connect timeout, pooled-connection lifetime), 10 s per request, User-Agent
  `Enigma.Launchpad/<version>`. For a site root (`scheme://host[:port]/`):
  1. GET the root page (`Accept: text/html`), reading at most 256 KB. If the site cannot be reached at all
     (connection failure, timeout), stop: `null`.
  2. If it answered with HTML, collect its icon links (`FaviconLinks`), best first, at most 3.
  3. Append `/favicon.ico`; try each candidate in turn until one is an image: success status, at most 1 MB, and a
     known image signature (PNG, ICO, GIF, JPEG, WebP, BMP — an HTML "soft 404" is not an icon).
- `FaviconLinks` — a pure parser: `<link>` tags whose `rel` holds `icon` or `apple-touch-icon(-precomposed)`
  (not `mask-icon`); `href` HTML-decoded and resolved against the page's final address; http/https only; SVG
  (by `type` or extension) skipped. Ranked by declared size — the smallest ≥ 48 px first, then the larger
  ones below 48 by descending size; an `icon` without `sizes` counts as 32 px, an apple-touch icon without
  `sizes` as 180 px. Document order breaks ties.
- Registered as a singleton (`AddLaunchpadApp`), built by a factory so the container owns and disposes it.

### Serving and tiles (PHASE02)

- `IWebsiteIcons` — `Task<IImage?> GetAsync(string url, CancellationToken cancellationToken = default)`.
- `WebsiteIcons` — the policy: the URL is re-parsed (`UrlNormalizer.TryParse`); anything not http/https gives
  `null`. Only the **site root** is ever handed to the fetcher — rebuilt from scheme, IDN host and port, so a
  shortcut's path, query and user info are never sent. One fetch per site for the session (shared in-flight,
  run on the thread pool; a caller's token only ends its own wait). Bytes are decoded to a `Bitmap` and scaled
  down to fit 48 px when larger. Any exception → `null`, logged at Warning.
- Tile wiring generalized: `GetProgramIconAsync` → `GetTargetIconAsync`; `ProgramIcon` / `HasProgramIcon` →
  `TargetIcon` / `HasTargetIcon`. `UrlsPageViewModel` overrides the hook with `IWebsiteIcons` (new constructor
  dependency). Directories stay unchanged.
- Test isolation: `WindowHarness` registers an offline `IWebsiteIconFetcher` (always `null`) before the caller's
  replacements, so no test reaches the network.

### Disk cache (PHASE03)

- `IWebsiteIconStore` / `WebsiteIconStore` — raw icon bytes per site under `<data directory>/icons/`, named by a
  SHA-256 hash of the site root, written atomically. An entry younger than 7 days is fresh.
- `WebsiteIcons` reads a fresh entry without any network request; otherwise it fetches, stores what it got, and —
  when the fetch yields nothing — falls back to the stale entry. An entry that does not decode is ignored.
- `AtomicFile.WriteAllBytesAsync` beside the text variant.
- README "Your data" mentions the `icons` folder: website icons, refreshed weekly, safe to delete.

## Phases

### PHASE01 — Favicon fetcher

**Status:** DONE

Steps:
1. `Services/FaviconLinks.cs` (internal static parser + ranking).
2. `Services/WebsiteIconFetcher.cs`: `IWebsiteIconFetcher`, `WebsiteIconFetcher` (handler/client factory, fetch
   flow, limits, signature sniffing, Debug/Warning logging), `IDisposable`.
3. Register it in `AddLaunchpadApp`.
4. Tests (fake `HttpMessageHandler`): link parsing (rel variants, quoting, entities, relative/absolute/
   protocol-relative hrefs, SVG and `mask-icon` skipped, non-http dropped), ranking; fetch: best link wins, falls
   back to the next candidate and to `/favicon.ico`, soft-404 HTML refused, oversized icon refused, unreachable
   site stops after one request, only the root and icon URLs are requested, no cookies sent, User-Agent set;
   DI resolves the fetcher.

Acceptance criteria:
- Given a site whose home page links a PNG icon, the fetcher returns that PNG's bytes; with no usable link it
  returns `/favicon.ico` when that is an image; otherwise `null`.
- It never requests anything but the site root and icon URLs, and never more than 1 + 4 requests per site.
- 0 warnings; suite green; no test touches the network.

### PHASE02 — URL tiles show website icons

**Status:** TODO

Steps:
1. `Services/WebsiteIcons.cs`: `IWebsiteIcons`, `WebsiteIcons` (policy, cache, decode, downscale, logging);
   registered as a singleton.
2. Rename the tile hook and properties to `GetTargetIconAsync` / `TargetIcon` / `HasTargetIcon` (view, tests).
3. `UrlsPageViewModel` override and constructor dependency; `PageHarness` gains a fake `IWebsiteIcons`.
4. `WindowHarness` registers an offline fetcher by default.
5. Tests: site root derivation (path, query, user info and fragment dropped; port kept; IDN host as punycode);
   one fetch per site for many URLs on it; non-http and invalid input → `null` without fetching; bytes decoded
   and larger icons scaled to 48 px (headless); undecodable bytes → `null` + Warning; URL tiles show the site
   icon, keep the globe without one, and swap it when an edit changes the site; directories never ask.

Acceptance criteria:
- A URL tile whose site has a fetchable icon shows it; any other URL tile keeps the globe.
- Application tiles behave as before.
- 0 warnings; suite green.

### PHASE03 — Website icon disk cache

**Status:** TODO

Steps:
1. `AtomicFile.WriteAllBytesAsync` (Core) + test.
2. `Services/WebsiteIconStore.cs`: `IWebsiteIconStore`, `WebsiteIconStore` (`IAppPaths`, `TimeProvider`);
   registered as a singleton.
3. `WebsiteIcons` consults the store as designed.
4. README "Your data": the `icons` folder.
5. Tests: store round-trip, freshness boundary, missing entry, distinct sites distinct files; `WebsiteIcons` uses a
   fresh entry without fetching, refreshes a stale one and stores it, falls back to a stale entry when the fetch
   fails, ignores an undecodable entry.

Acceptance criteria:
- A site fetched once is shown from disk, with no request, for 7 days, including after a restart.
- Offline, a site seen before still shows its last icon.
- 0 warnings; suite green.

## Out of scope

- An opt-out setting for website icons; icons in the export/import tree or the shortcut editor.
- SVG icons (would need a new dependency); web app manifests (`rel="manifest"`); per-page icons (only the site
  root is consulted, by design).
- Pruning cache entries of sites no longer in any profile.
- Directory icons.

## Decisions taken autonomously

| Question                 | Chosen                                                                                   | Why                                                                                              | Alternatives rejected                                             |
|--------------------------|------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------|-------------------------------------------------------------------|
| Surface                  | URL page tiles                                                                           | "The same" as application tiles                                                                  | Also the selection tree                                           |
| Icon source              | The site itself: home-page icon links, then `/favicon.ico`                               | No third party learns the user's sites; the site's own best icon                                 | Google / DuckDuckGo favicon services; `/favicon.ico` only          |
| What is requested        | Site root and icon URLs only; no cookies; no user info                                   | An automatic GET of an imported link's own path could trigger an action on that site or device  | Fetching the shortcut's exact URL                                 |
| Formats                  | PNG, ICO, GIF, JPEG, WebP, BMP, recognised by signature; SVG skipped                     | What Skia decodes; servers often mislabel content types                                          | Adding an SVG renderer; trusting `Content-Type`                   |
| Candidate ranking        | Smallest ≥ 48 px, else largest; unsized icon = 32, unsized apple-touch = 180             | 48 px source keeps 24 px tiles sharp to 200 %; simple and predictable                            | Always the largest; always `/favicon.ico`                         |
| Limits                   | 10 s/request, 5 s connect, ≤ 5 redirects, 256 KB page, 1 MB icon, ≤ 3 links + favicon.ico | Bounded time, memory and requests per site                                                       | No limits; a global concurrency limiter (not needed at this scale) |
| HTTP plumbing            | Fetcher-owned `HttpClient` on `SocketsHttpHandler`, container-disposed                   | Recommended long-lived pattern; no new package                                                   | `IHttpClientFactory` (new package; typed clients in singletons)  |
| User-Agent               | `Enigma.Launchpad/<version>`                                                             | Honest identification                                                                            | A browser User-Agent                                              |
| Caching                  | Session cache per site + 7-day disk cache with stale fallback                            | Instant icons at start, offline support, each site contacted at most weekly                      | Session only (refetch every start); no expiry                     |
| Tile wiring              | Generalize to `TargetIcon` / `GetTargetIconAsync`                                        | One mechanism for programs and sites; accurate names                                             | A second, URL-specific property set                               |
| Opt-out setting          | Not in this item                                                                         | Not asked for; exposure bounded by the request rules                                             | A Settings toggle now                                             |
| Logging                  | Network failures at Debug, unexpected exceptions at Warning                              | Offline and missing icons are routine                                                            | Everything at Warning                                             |
| Test isolation           | Fakes everywhere; `WindowHarness` offline by default; one throwaway real-network probe    | A suite that needs the internet is a flaky suite                                                 | Live-network tests                                                |
| Item shape               | One `FEATURE`, three phases (fetcher, tiles, disk cache)                                 | Each phase is one reviewable commit; the cache can be judged on its own                          | One or two larger devs                                            |
