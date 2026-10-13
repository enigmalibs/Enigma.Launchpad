# FEATURE-1178-PHASE01 — Favicon fetcher

## Summary

The desktop app can now download a website's icon, ready for PHASE02 to put on URL tiles. Nothing visible
changes yet.

- `FaviconLinks` (internal, pure) — finds the icons a page declares: `<link>` tags whose `rel` holds `icon`
  (`icon`, `shortcut icon`, `alternate icon`) or `apple-touch-icon(-precomposed)`; `mask-icon`, manifests and other
  links are ignored. Each `href` is HTML-decoded and resolved against the page (relative, root-relative,
  protocol-relative). Only http/https results are kept (no `data:` or `javascript:`), and SVG icons — by `type`
  or extension — are dropped. Ranking: the smallest icon of at least 48 px first, then the largest below 48 px. An
  unsized `icon` counts as 32 px and an unsized apple-touch icon as 180 px; page order breaks ties; repeats count
  once.
- `IWebsiteIconFetcher` / `WebsiteIconFetcher` — for a site root:
  1. GET the home page (`Accept: text/html`), reading at most 256 KB. A network failure or timeout ends there:
     `null` after one request.
  2. If the answer is HTML, take its best three icon links.
  3. Add `/favicon.ico`, resolved against the address the redirects ended at.
  4. Try each candidate (`Accept: image/*`). The first one that succeeds, is ≤ 1 MB (announced or counted while
     reading) and starts with a PNG, ICO, GIF, JPEG, WebP or BMP signature wins. An HTML "soft 404" served with a
     success status is refused.
  - It owns one `HttpClient` over a `SocketsHttpHandler`: no cookies, no credentials, ≤ 5 redirects, automatic
    decompression, 5 s connect timeout, 5 min pooled-connection lifetime, 10 s per request, User-Agent
    `Enigma.Launchpad/0.1.0`.
  - Routine failures are logged at Debug. The caller's own cancellation propagates.
- Registered as a singleton; the container disposes it with its connection pool.

## Files

Created:
- `src/Enigma.Launchpad.Desktop/Services/FaviconLinks.cs`
- `src/Enigma.Launchpad.Desktop/Services/WebsiteIconFetcher.cs` — `IWebsiteIconFetcher`, `WebsiteIconFetcher`.
- `tests/Enigma.Launchpad.Desktop.UnitTests/FaviconLinksTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/WebsiteIconFetcherTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/StubHttpHandler.cs` — `StubHttpHandler` (addresses →
  answers, recorded requests, unannounced-length bodies) and `ImageBytes`.

Modified:
- `src/Enigma.Launchpad.Desktop/DependencyInjection/ServiceCollectionExtensions.cs` — registration.
- `tests/Enigma.Launchpad.Desktop.UnitTests/CompositionRootTests.cs` — `IWebsiteIconFetcher` resolves.

## Deviations & follow-ups

- No deviation from the plan.
- Fix budget: 1 of 3 cycles used. The stub HTTP handler didn't set `response.RequestMessage` the way the real
  handler does, so the redirect test couldn't see where redirects ended. The stub now sets it. No production
  code changed.
- Live check (throwaway test, deleted, never committed): the real fetcher was run against nine sites. GitHub,
  Avalonia docs, Wikipedia, Google, Microsoft Learn, Hacker News, Stack Overflow and NuGet each returned an
  icon (ICO or PNG) in 0.1–2.2 s. `example.com`, which has none, returned `null`.
- The site is reached over the system proxy (the handler's default). A proxy that requires authentication isn't
  supported — no credentials are ever sent — and such sites keep the globe.
- Documentation sweep: nothing changed. Nothing visible changes in this phase.
- Line endings: every touched file is LF, per `.gitattributes`.

## Decisions taken at build time

| Question                                   | Chosen                                                             | Why                                                                                         |
|--------------------------------------------|--------------------------------------------------------------------|---------------------------------------------------------------------------------------------|
| Test seam                                  | An internal constructor taking the `HttpMessageHandler`            | The container only sees the public constructor; tests swap the transport, nothing else      |
| Base for links and `/favicon.ico`          | Where redirects ended (`response.RequestMessage.RequestUri`)       | `google.com` → `www.google.com`: the icon lives on the final host                           |
| Page decoding                              | UTF-8 with replacement, first 256 KB                               | Icon links are ASCII; a Latin-1 page still reads correctly where it matters                 |
| What counts as HTML                        | `text/html` or `application/xhtml+xml`                             | Anything else (JSON, images, no type) goes straight to `/favicon.ico`                       |
| Oversized bodies                           | Rejected on `Content-Length`, else when reading passes the limit   | Never buffers more than the limit plus one chunk                                            |
| Network failure vs. no icon                | Exceptions at Debug; nothing at Warning                            | Offline and icon-less sites are routine                                                     |
| Regex flavour                              | `[GeneratedRegex]`                                                 | Compiled at build time; no runtime regex construction                                       |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 518 passed, 0 failed, 0 skipped (464 before; 54 added: 27 in
  `FaviconLinksTests`, 26 in `WebsiteIconFetcherTests`, 1 in `CompositionRootTests`). No test touches the
  network.
