# FEATURE-003B — Icon set that fills the frame

**Item:** FEATURE-003B — Icon set that fills the frame
**Branch:** `feature/feature-003b-icon-fills-frame`
**Run:** feature/2026-11-04-icons-release

## Summary

The application icon is replaced by the new set from `Branding/Apps/final3/launchpad` (`export.py final3`). It is
1.5.0's rocket (LP-A2, *Soft* palette, transparent background, the window a hole in the body), **scaled by 1.4452 and
centred so it fills the frame**: its longest side spans 16 to 240 of the 256-unit view box, where it was much smaller
before. Every copy is the delivered file byte for byte:

- `Assets/app.ico`: the new ten-frame icon (16, 20, 24, 32, 40, 48, 64, 96, 128, 256; 32-bit RGBA PNG frames). It
  is the executable's icon (the taskbar's), the window's icon and the `app.ico` written beside the executable, which
  `buildmsi.ps1` takes as the MSI's product icon and its shortcuts' icon;
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
  - `docs/roadmap.md`, `docs/plan/FEATURE-003B.md`: statuses.
- Created: `docs/done/FEATURE-003B.md`

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Prose about the icon's artwork | Nothing to change | No runbook, comment or test describes how the rocket sits in its frame; the README's *What's new in 1.5* callout and the 1.3.0 / 1.5.0 notes describe earlier releases, and the callout is rewritten by FEATURE-8698 |
| The Windows installer | No change | `buildmsi.ps1` reads `app.ico` from the publish folder, so the next MSI carries the new icon |

## Deviations & follow-ups

- None from the plan.
- The source directory `Branding/Apps/final3/launchpad` is outside this repository and was left alone.
- Line endings: the delivered SVG has CRLF line endings. `.gitattributes` (`* text=auto eol=lf`) stores it as LF,
  so the committed SVG differs from the source only in line endings. The binaries (`.ico`, `.png`) are byte for
  byte. No action taken (recommendation only, as for FEATURE-BF01: have `export.py` write LF).
- Windows caches icons: after installing 1.6.0, a pinned taskbar shortcut may show the previous icon until the
  icon cache refreshes (sign out and in). Mentioned for the release notes; nothing to do in the repository.

## Build/test evidence

**The run's baseline**, taken once on `feature/2026-11-04-icons-release` at `0659f01` (`develop` @ `086cc0d` plus
the planning commit), before this first dev. Later devs of the run cite it:

- `dotnet build Enigma.Launchpad.slnx -c Release --no-incremental`: succeeded, **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Launchpad.slnx -c Release`: 1,028 total, 1,027 passed, 1 skipped (the Unix-only
  `PathCommandResolverTests.Elsewhere_AFileThatMayNotBeExecuted_IsNoProgram`), **0 failed**.

**This dev:**

- Planning check: the new `.ico` holds ten 32-bit RGBA PNG frames, each byte for byte its `png/` file.
- `cmp`: each of the 11 destination files is identical to its source in `Branding/Apps/final3/launchpad`.
- `dotnet build Enigma.Launchpad.slnx -c Release --no-incremental`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Launchpad.slnx -c Release`: 1,028 total, 1,027 passed, 1 skipped, 0 failed.
- `AppIconTests` on their own: 4/4 passed. They read the icon embedded in the Release assembly, so they prove it is
  the new one: its frames are byte for byte the new Linux PNGs, and its 256 frame is the new `app-256.png`.
- The `app.ico` beside the Release executable is the new file (`cmp`).
- Fix budget: 0 cycles used.

## Documentation sweep

- Nothing stale: the README and `docs/RELEASE.md` describe the icon's sizes and places, which did not change. The
  new look goes into the 1.6.0 release notes (FEATURE-8698).
