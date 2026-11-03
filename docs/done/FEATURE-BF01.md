# FEATURE-BF01 — New icon set without the tile

**Item:** FEATURE-BF01 — New icon set without the tile
**Branch:** `feature/feature-bf01-new-icon-set`
**Run:** feature/2026-11-03-icons-msi-release

## Summary

The application icon is replaced by the new set from `Branding/Apps/final2/launchpad` (`export.py final2`): the
same rocket (LP-A2, *Soft* palette) **without the dark rounded tile**, so the background is transparent. The rocket's
window, a dark disc before, is now a hole in its body, so the background shows through it too. Every copy is the
delivered file byte for byte:

- `Assets/app.ico`: the new ten-frame icon (16, 20, 24, 32, 40, 48, 64, 96, 128, 256; 32-bit RGBA PNG frames). It
  is the executable's icon, the window's icon and the `app.ico` written beside the executable;
- `Assets/app-256.png`: the new 256 frame, shown by the About dialog;
- `packaging/icon/enigma-launchpad.svg`: the new master, its header comment as the export writes it;
- `packaging/linux/icons/`: the eight sizes `install.sh` installs.

The icon's shape is unchanged (same sizes, same encoding, same file names), so no code, script or test changes.

## Files/modules touched

- Modified:
  - `src/Enigma.Launchpad.Desktop/Assets/app.ico`
  - `src/Enigma.Launchpad.Desktop/Assets/app-256.png`
  - `packaging/icon/enigma-launchpad.svg`
  - `packaging/linux/icons/enigma-launchpad-{16,24,32,48,64,96,128,256}.png`
  - `docs/roadmap.md`, `docs/plan/FEATURE-BF01.md`: statuses.
- Created: `docs/done/FEATURE-BF01.md`

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Prose about the tile | Nothing to change | No README, runbook, comment or test describes the icon's background; only the 1.3.0 release notes do, and they describe what 1.3.0 shipped |

## Deviations & follow-ups

- The source directory `Branding/Apps/final2/launchpad` is outside this repository and was left alone.
- Line endings: the delivered SVG has CRLF line endings. `.gitattributes` (`* text=auto eol=lf`) stores it as LF,
  so the committed SVG differs from the source only in line endings. The binaries (`.ico`, `.png`) are byte for
  byte. No action taken (recommendation only: have `export.py` write LF).

## Build/test evidence

**The run's baseline**, taken once on `feature/2026-11-03-icons-msi-release` at `641ebc3` (`develop` @ `af59e34`
plus the planning commit), before this first dev. Later devs of the run cite it:

- `dotnet build Enigma.Launchpad.slnx -c Release`: succeeded, **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Launchpad.slnx -c Release`: 1,025 total, 1,024 passed, 1 skipped (the Unix-only
  `PathCommandResolverTests.Elsewhere_AFileThatMayNotBeExecuted_IsNoProgram`), **0 failed**.

**This dev:**

- `cmp`: each of the 11 destination files is identical to its source in `Branding/Apps/final2/launchpad`.
- `dotnet build Enigma.Launchpad.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Launchpad.slnx -c Release`: 1,025 total, 1,024 passed, 1 skipped, 0 failed.
- `AppIconTests` on their own: 4/4 passed. They read the icon embedded in the Release assembly, so they prove it is
  the new one: its frames are byte for byte the new Linux PNGs, and its 256 frame is the new `app-256.png`.
- The `app.ico` beside the Release executable is the new file (`cmp`).
- Fix budget: 0 cycles used.

## Documentation sweep

- Nothing stale: the README and `docs/RELEASE.md` describe the icon's sizes and places, which did not change. The
  new look goes into the 1.5.0 release notes (FEATURE-0859).
