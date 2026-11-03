# FEATURE-E90D-PHASE01 — Vendor the Enigma.Msi worker

**Item:** FEATURE-E90D — Windows MSI build script
**Branch:** `feature/feature-e90d-phase01-vendor-msi-worker`
**Run:** feature/2026-11-03-icons-msi-release

## Summary

The repository now carries Enigma.Msi's headless worker CLI in `packaging/windows/Enigma.Msi.Worker/`, for
PHASE02's `buildmsi.ps1` to run (`Enigma.Msi.Worker.exe build <profile>`). It is Enigma.Msi `1.7.0` (`a7a1bfb`) built
against WixSharp_wix4 2.14.2: the 20 files of its `bin/Release/net472/` output, without the PDBs, the API XML and
`.wixsharp/` (3.4 MB). It is the same set, with the same bytes, as Enigma.GitClient's FEATURE-EAB7-PHASE01.

- A `README.md` in the folder gives, as GitClient's does with this repository's MSI name:
  - its source;
  - the CLI's exit codes;
  - the requirements: .NET Framework 4.7.2 and the WiX CLI, with WiX 7's EULA;
  - the components' licences: Enigma.Msi, WixSharp and .NET are MIT; the WiX 4.0.1 assemblies are MS-RL;
  - how to refresh it.
- `.gitignore` ignores `packaging/windows/Enigma.Msi.Worker/.wixsharp/`. WixSharp writes it beside the worker on its
  first build. `git clean -dxf` removes it, and the next build writes it again.

## Files/modules touched

- Created:
  - `packaging/windows/Enigma.Msi.Worker/` (20 binaries: `Enigma.Msi.Worker.exe`, `Enigma.Msi.Worker.exe.config`,
    `Enigma.Msi.dll`, the `WixSharp*`, `WixToolset.*`, `System.*`, `Microsoft.Bcl.AsyncInterfaces.dll` and
    `mbanative.dll`)
  - `packaging/windows/Enigma.Msi.Worker/README.md`
  - `docs/done/FEATURE-E90D-PHASE01.md`
- Modified:
  - `.gitignore`
  - `docs/roadmap.md`, `docs/plan/FEATURE-E90D.md`: statuses.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Which build to copy | Enigma.Msi's existing `bin/Release/net472/` output | It is the `1.7.0` tag's build GitClient vendored (byte for byte its copy), and the Enigma.Msi tree is clean. Rebuilding would have written into another repository |
| The README | GitClient's, with `Enigma.Launchpad.X.Y.Z.msi` | "Exactly like done by Enigma.GitClient"; the only line that names the product |

## Deviations & follow-ups

- `Enigma.Msi.Worker.exe.config` is the only file that will differ from its source once committed, and only in line
  endings: the build writes CRLF, and `.gitattributes` (`* text=auto eol=lf`) stores it as LF. .NET Framework reads
  either. Every `.exe`/`.dll` is byte for byte (`.gitattributes` marks them binary).
- Enigma.Msi's build folder holds a `.wixsharp/` from an earlier build there; it was not copied.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-BF01.md` (0 warnings; 1,025 tests, 1,024 passed, 1 skipped, 0 failed).
- `cmp` of each copied file against its source: all 20 identical before staging.
- `git status --porcelain --ignored -uall packaging/windows/Enigma.Msi.Worker/`: the 21 files (20 binaries and the
  README) are untracked, none ignored. `git check-ignore` matches `.wixsharp/…` to the new rule (`.gitignore:369`).
- `Enigma.Msi.Worker.exe` with no arguments printed "No arguments were given." and the two usage forms, exit code
  **2**.
- `dotnet build Enigma.Launchpad.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Launchpad.slnx -c Release`: 1,025 total, 1,024 passed, 1 skipped, 0 failed.
- Fix budget: 0 cycles used.

## Documentation sweep

- Nothing stale yet: the README and `docs/RELEASE.md` change with the script itself (PHASE02).
