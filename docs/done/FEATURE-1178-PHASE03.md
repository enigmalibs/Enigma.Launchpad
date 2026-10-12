# FEATURE-1178-PHASE03 — Website icon disk cache

## Summary

Website icons now survive restarts. A site is asked for its icon at most once a week, and a site that can't be
reached keeps the icon it last had.

- `IWebsiteIconStore` / `WebsiteIconStore` — one file per site in `<data directory>/icons/`, holding the icon
  file exactly as the site served it. The file name is the first 128 bits of the SHA-256 of the site root
  (`<hex>.icon`). Writes are atomic. An entry is fresh for 7 days after it was written (`TimeProvider`).
- `WebsiteIcons`, per site, once per session:
  1. Read the stored entry and decode it.
  2. If it's fresh and decodes, show it with no network request.
  3. Otherwise fetch. A decodable icon is shown and stored.
  4. If the fetch brings nothing — offline, or the site dropped its icon — show the stored icon, however old.
  - No step throws. A stored entry that won't read or decode is ignored (Debug log); a failed fetch, an
    undecodable fetched icon, or a failed store is logged at Warning. A failed store still shows the fetched
    icon.
- `AtomicFile.WriteAllBytesAsync` (Core) beside the text variant. Both now share one
  write-beside-then-move helper.
- README "Your data": the `icons` folder — what it holds, what is asked of a site, the weekly refresh, and that
  deleting it is safe.

## Files

Created:
- `src/Enigma.Launchpad.Desktop/Services/WebsiteIconStore.cs` — `StoredWebsiteIcon`, `IWebsiteIconStore`,
  `WebsiteIconStore`.
- `tests/Enigma.Launchpad.Core.UnitTests/Persistence/AtomicFileTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/WebsiteIconStoreTests.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/FakeWebsiteIconStore.cs`
- `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/FixedTimeProvider.cs` — same as the Core tests' one.

Modified:
- `src/Enigma.Launchpad.Core/Persistence/AtomicFile.cs` — `WriteAllBytesAsync`; shared helper.
- `src/Enigma.Launchpad.Desktop/Services/WebsiteIcons.cs` — store first, fetch, store, stale fallback; remarks.
- `src/Enigma.Launchpad.Desktop/DependencyInjection/ServiceCollectionExtensions.cs` — store registered.
- `tests/Enigma.Launchpad.Desktop.UnitTests/WebsiteIconsTests.cs` — cache behaviour, including a restart over a
  real on-disk store.
- `README.md` — "Your data".

## Deviations & follow-ups

- No deviation from the plan.
- Fix budget: none used — the first build and test run were green. One test (the restart one) was added after
  that, and the suite ran green twice more.
- `WriteAllTextAsync` now delegates to the shared helper. Its behaviour is unchanged: UTF-8 without BOM, write
  beside then move, temporary file always removed. A new text test pins that down, and the existing store tests
  still pass.
- Follow-up (plan: out of scope): pruning icons of sites that are no longer in any profile. Each file is a few
  KB.
- Documentation sweep: the README edit above was planned (step 4). Nothing else became wrong.
- Line endings: every touched file is LF, per `.gitattributes`.

## Decisions taken at build time

| Question                              | Chosen                                                            | Why                                                                                       |
|---------------------------------------|-------------------------------------------------------------------|-------------------------------------------------------------------------------------------|
| What is stored                        | The icon file as served, not a re-encoded bitmap                  | No encoder round-trip; the same decode path as a fresh fetch                              |
| Freshness clock                       | The file's last-write time against `TimeProvider`                 | No sidecar metadata; the atomic move keeps the time it was written                        |
| File name                             | First 16 bytes of SHA-256(site root), lowercase hex, `.icon`      | Valid on every file system whatever the host or port; collisions are negligible           |
| Where store failures go               | `WebsiteIcons` catches them; the store itself throws              | The store stays a plain I/O seam; the policy decides what a failure means                 |
| Stale icon when the fetch succeeds    | Disposed, replaced by the fetched one                             | The newer icon wins; the old bitmap is not held                                           |
| Stored entry that does not decode     | Ignored at Debug, then fetched                                    | A damaged cache file repairs itself on the next fetch                                     |
| Restart acceptance test               | Two `WebsiteIcons` over one real `WebsiteIconStore`               | Tests the "after a restart" criterion directly                                            |

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx --no-incremental` — 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx` — 559 passed, 0 failed, 0 skipped (542 before; 17 added: 3 in
  `AtomicFileTests`, 6 in `WebsiteIconStoreTests`, 8 in `WebsiteIconsTests`). Two more `--no-build` runs were
  also all green. No test touches the network.
