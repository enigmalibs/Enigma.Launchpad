# FEATURE-03CF — Program icons on application tiles

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-03cf-phaseNN-<slug>` (one per phase)
**Run:** vibe/2026-10-12-app-list-icons

## Objective

On the **Applications** page, each tile shows the program's own icon — the one Explorer shows for that file —
instead of the generic `AppWindow` glyph, whenever the platform can supply it. When it cannot (the program is
missing, the file has no icon, the platform offers no way to read one), the tile keeps the generic glyph, exactly
as today.

## Context & constraints

- Tiles are drawn by `CategoryPageView.axaml`; `ShortcutViewModel.Icon` picks a Phosphor glyph per category
  (`AppWindow`, `Folder`, `Globe`). FEATURE-5B69 left "extracted executable icons" out of scope and recorded it as
  a follow-up (`docs/done/FEATURE-5B69-PHASE04.md`).
- The app is cross-platform (`net10.0`; Windows, Linux, macOS). An executable carries a portable icon on none of
  them except Windows, where the shell resolves `.exe`, `.lnk`, scripts and documents alike.
- `Enigma.Launchpad.Desktop.csproj` already sets `BuiltInComInteropSupport`; the solution has no native interop
  yet. `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` are on solution-wide.
- `ShortcutValidator` accepts any fully qualified path — UNC paths included — and an imported file may carry
  paths that do not exist on this machine.
- A spike (`IShellItemImageFactory.GetImage`, `SIIGBF_ICONONLY`, 48 × 48) confirmed: it works on thread-pool
  (MTA) threads; it returns a 32 bpp top-down-readable DIB with **straight** (unpremultiplied) alpha; a missing
  file fails with `0x80070002`; `.lnk` files resolve to their target's icon.

## Design

### Reading an icon (Windows)

- `IProgramIconReader` — the OS seam: `ProgramIconPixels? Read(string path, int size)`; `null` when no icon can
  be had. `ProgramIconPixels` is a record of width, height and BGRA bytes (straight alpha, top row first).
- `ShellProgramIconReader` — on Windows: `SHCreateItemFromParsingName` → `IShellItemImageFactory.GetImage`
  (`SIIGBF_ICONONLY`) → `GetDIBits` (32 bpp, negative height) → pixels; every handle and COM object released.
  An HRESULT failure, or an image whose alpha is entirely zero, gives `null`. On any other platform it returns
  `null` without doing anything. `DllImport` + `[ComImport]`, no `unsafe`; Windows-only members carry
  `[SupportedOSPlatform("windows")]` behind an `OperatingSystem.IsWindows()` guard.

### Serving icons (all platforms)

- `IProgramIcons` — `Task<IImage?> GetAsync(string path, CancellationToken cancellationToken = default)`.
- `ProgramIcons` — the policy:
  - a UNC / device path (`\\…`, `//…`) and a path that is not an existing file are answered `null` without
    touching the shell — showing a page must never reach out to a network host named in an imported file;
  - otherwise the reader runs on the thread pool, at 48 px, and its pixels become an Avalonia `Bitmap`
    (`Bgra8888`, `AlphaFormat.Unpremul`);
  - results — icons and `null`s — are cached per path for the session (case-insensitive on Windows); concurrent
    requests for one path share one read; the caller's token only stops its own wait;
  - an exception while reading or converting is logged at Warning and answered `null`: an icon is cosmetic.
- Both registered as singletons in `AddLaunchpadApp`.

### Tiles

- `CategoryPageViewModel` gains a virtual hook, `GetProgramIconAsync(string target)`, answering `null`;
  `ApplicationsPageViewModel` overrides it with `IProgramIcons` (new constructor dependency). Directories and URLs
  are untouched.
- `ShortcutViewModel` exposes `ProgramIcon` (`IImage?`) and `HasProgramIcon`. It asks for the icon when it is
  created and again when an edit changes the target; it clears the previous icon on such an edit, and ignores an
  answer that arrives for a target it no longer has.
- The tile's plate shows an `Image` (24 px, high-quality scaling) when `HasProgramIcon`, else the Phosphor glyph.
  The image is decorative: the tile's automation name stays the shortcut's name.

## Phases

### PHASE01 — Windows program icon service

**Status:** DONE

Steps:
1. `Services/ProgramIconPixels.cs`, `Services/IProgramIconReader.cs` + `ShellProgramIconReader` (interop as above).
2. `Services/IProgramIcons.cs` + `ProgramIcons` (policy, cache, conversion, logging).
3. Register both in `AddLaunchpadApp`.
4. Tests: UNC and missing paths answered `null` without reading; one read per path (case-insensitive on Windows)
   for sequential and concurrent requests; a `null` read is cached; a throwing reader gives `null` and a Warning;
   pixels become a `Bitmap` of the right size (headless); on Windows, the real reader returns a 48 × 48 icon for
   `explorer.exe` with some opaque pixels and `null` for a missing file (skipped elsewhere); DI resolves
   `IProgramIcons` as a singleton.

Acceptance criteria:
- On Windows, `IProgramIcons.GetAsync` yields an image for an existing program and `null` for a missing one or a
  UNC path; elsewhere it yields `null`.
- No shell call ever runs on the calling thread.
- 0 warnings; suite green.

### PHASE02 — Tiles show program icons

**Status:** DONE

Steps:
1. `CategoryPageViewModel.GetProgramIconAsync` hook; `ApplicationsPageViewModel` override and constructor
   dependency; test harness updated.
2. `ShortcutViewModel.ProgramIcon` / `HasProgramIcon`, loading on creation and on a target change, stale answers
   ignored.
3. `CategoryPageView.axaml` + `Themes/Styles.axaml`: the image in the tile plate, glyph as fallback.
4. Tests: an application tile gets the icon its target resolves to; no icon keeps `HasProgramIcon` false;
   directory and URL tiles never ask; an edit that changes the target swaps the icon and a late answer for the old
   target is ignored; an edit that keeps the target does not ask again; headless view shows the `Image` for a tile
   with an icon and the glyph for one without.

Acceptance criteria:
- On Windows, an application tile whose program exists shows that program's icon; a missing program, a UNC path,
  and every tile on Linux/macOS show the generic glyph.
- Opening the page never waits for icons: tiles appear at once and their icons fill in.
- 0 warnings; suite green.

## Out of scope

- Program icons on Linux (freedesktop `.desktop` / icon-theme lookup) and macOS (`.app` bundle `.icns`).
- Icons in the export/import selection tree, in the shortcut editor, or for directory and URL tiles (favicons).
- Persisting icons to disk; refreshing an icon whose program changed while the app runs (it refreshes on restart).
- Custom per-shortcut icons.

## Decisions taken autonomously

| Question                      | Chosen                                                                     | Why                                                                                      | Alternatives rejected                                         |
|-------------------------------|----------------------------------------------------------------------------|------------------------------------------------------------------------------------------|---------------------------------------------------------------|
| Which surfaces                | Applications page tiles                                                    | "The apps lists" are that page's groups; narrowest reading that delivers the request     | Also the selection tree; also directories                     |
| Which platforms               | Windows through the shell; Linux/macOS keep the glyph                      | Only Windows gives an executable a portable icon; "when possible" asks for best effort   | `.icns` parsing for macOS bundles; freedesktop lookups now    |
| Windows API                   | `IShellItemImageFactory`, icon only, 48 px                                 | What Explorer shows, for exe/lnk/scripts/documents; any size; validated by a spike       | `SHGetFileInfo` (fixed 32 px, HICON + mask); `ExtractIconEx` (PE files only); `System.Drawing.Common` (new dependency) |
| Interop style                 | `DllImport` + `[ComImport]`, no `unsafe`                                   | COM interop is already enabled; no new build setting                                     | `LibraryImport` / `GeneratedComInterface` (needs `AllowUnsafeBlocks`) |
| Threading                     | Thread pool; the tile shows the glyph until the icon arrives               | Shell reads can be slow (cold cache, slow disks); the UI never waits                     | Reading on the UI thread; a dedicated STA thread (spike shows MTA works) |
| Cache                         | Per path, session-long, failures included, shared in-flight reads          | One exe in many groups/profiles is read once; a profile switch costs nothing             | No cache; a disk cache; re-probing failures                   |
| Network paths                 | UNC paths never probed                                                     | An imported file could otherwise make the page contact an attacker's SMB host (NTLM leak) | Probing every path like Explorer does                         |
| Failure handling              | `null` → glyph; unexpected exceptions logged at Warning                    | An icon is cosmetic and must never break a tile or the page                              | Surfacing errors to the user                                  |
| Visual                        | Keep the 36 px plate; icon at 24 px, high-quality scaling                  | No layout shift; 48 px source stays crisp up to 200 % scaling                            | Dropping the plate; 32 px icon                                |
| Where the VM gets the icon    | Virtual page hook overridden by `ApplicationsPageViewModel`                | Only the applications page knows about program icons; other pages unchanged             | A dependency on the shared base for all three pages           |
| Seams                         | `IProgramIconReader` (OS) + `IProgramIcons` (policy), singletons           | Mirrors `IProcessStarter`; the policy is tested without the OS                           | One class with no seam                                        |
| Item shape                    | One `FEATURE`, two phases (service, then tiles)                            | Each phase is one reviewable commit; the interop is reviewed on its own                  | One dev                                                       |
