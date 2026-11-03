# FEATURE-E90D-PHASE02 — The buildmsi script

**Item:** FEATURE-E90D — Windows MSI build script
**Branch:** `feature/feature-e90d-phase02-buildmsi-script`
**Run:** feature/2026-11-03-icons-msi-release

## Summary

`packaging/windows/buildmsi.ps1` builds the Windows installer in one command, from any directory. It is
Enigma.GitClient's script (FEATURE-EAB7-PHASE02) with Launchpad's four constants and names:

1. It checks that `git`, `dotnet`, `wix` and the vendored worker are there, **before** anything is cleaned.
2. It reads the version from `Directory.Build.props` and checks it is `X.Y.Z`.
3. `git -C <root> clean -dxf`.
4. `publish.ps1`, the existing `-c Release -r win-x64 -p:PublishReadyToRun=true` publish. Then it checks that the exe
   and `app.ico` are in the publish folder.
5. It writes `artifacts/Enigma.Launchpad.X.Y.Z.msipkg.json`, an Enigma.Msi profile:
   - absolute paths, a new `productId` each build;
   - upgrade code `b4c2d5a2-9182-4c17-b570-c6019eb5ec92`;
   - "Enigma Launchpad", "Josué Clément", `PerMachine`, `%ProgramFiles%\Enigma Launchpad`;
   - `compression` `None`;
   - `app.ico` as the Control Panel icon and both shortcuts' icon;
   - shortcuts in `%Desktop%` and `%ProgramMenu%` on `[INSTALLDIR]\Enigma.Launchpad.Desktop.exe`.
6. It runs `Enigma.Msi.Worker.exe build` on that profile into `artifacts/Enigma.Launchpad.X.Y.Z.msi`.

Every failure prints `buildmsi: <reason>` and exits non-zero, with the failing tool's exit code when there is one.
`buildmsi.bat` runs it from `cmd` with `-ExecutionPolicy Bypass` and passes the exit code on.

The upgrade code is Launchpad's own: the one the installed 1.4.0 MSI carries, so the next MSI upgrades it instead of
installing beside it. `WindowsInstallerScriptTests` pins it, the product name and the assembly.

## Files/modules touched

- Created:
  - `packaging/windows/buildmsi.ps1`
  - `packaging/windows/buildmsi.bat`
  - `tests/Enigma.Launchpad.Desktop.UnitTests/WindowsInstallerScriptTests.cs`
  - `docs/done/FEATURE-E90D-PHASE02.md`
- Modified:
  - `README.md`, `docs/RELEASE.md`
  - `docs/roadmap.md`, `docs/plan/FEATURE-E90D.md`: statuses.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| How to write the scripts | GitClient's two files with `Enigma Git Client` → `Enigma Launchpad`, `Enigma.GitClient` → `Enigma.Launchpad` and the upgrade code replaced; nothing else | "Exactly like done by Enigma.GitClient"; `diff` against GitClient's shows only those lines |
| Where the pins live | One Desktop class, `WindowsInstallerScriptTests`, beside `PackagingScriptsTests` | Launchpad has no display-name or assembly-name consistency suite to extend; the Desktop suite reaches `ProductInformation` and the app's assembly, and runs here |
| The assembly name in the pin | `typeof(App).Assembly.GetName().Name` | Ties the script to the assembly the build really produces, as GitClient's `AssemblyName` does |
| `buildmsi.bat` line endings in the working tree | CRLF | `.gitattributes` makes `*.bat` CRLF; `publish.bat` is CRLF |

## Deviations & follow-ups

- **Follow-up: the MSI is 174 MB** because the publish folder carries native PDBs (`libSkiaSharp.pdb`,
  `libHarfBuzzSharp.pdb`), as GitClient's does. Leaving them out would make the MSI much smaller and its build
  faster, but it changes the publish, so it is not done here.
- **A 32-bit package.** Enigma.Msi has no platform setting, so the MSI's template is `Intel;1033`, and
  `%ProgramFiles%` is `Program Files (x86)` on 64-bit Windows. The installed 1.4.0 sits under the same
  `ProgramFilesFolder`. `docs/RELEASE.md` says so.
- **Compression `None`**, as GitClient's script. The installed 1.4.0 MSI was compressed; an uncompressed MSI installs
  the same files.
- **WixSharp's "source base directory"** is the caller's current directory (it printed the real repository's path). It
  resolves relative paths only; the profile's are absolute. The real repository had no new file after the build.
- **Not run, by design:** installing, launching or uninstalling the MSI.
- Line endings: `buildmsi.bat` is CRLF as `.gitattributes` asks; the `.ps1` is LF and ASCII.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-BF01.md` (0 warnings; 1,025 tests, 1,024 passed, 1 skipped, 0 failed).
- `dotnet build Enigma.Launchpad.slnx -c Release`: **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.Launchpad.slnx -c Release`: **1,028** total (the baseline's 1,025 plus the 3 new
  tests), 1,027 passed, 1 skipped (the Unix-only resolver test), 0 failed. `WindowsInstallerScriptTests` on their
  own: 3/3 passed.
- **End to end, in a scratch clone** of this branch with the dev's tree committed there (the run may not `git clean`
  the user's repository):
  - **No `wix` on the `PATH`:** `buildmsi: wix is not on the PATH: dotnet tool install --global wix.`, exit code 1,
    and an untracked canary file was still there, so nothing was cleaned.
  - **`buildmsi.bat`:**
    - git removed the canary file; the publish ran; the worker built the MSI;
    - exit code **0**, in **19 s** wall time (restore, R2R publish and MSI);
    - `artifacts/` holds `Enigma.Launchpad.1.4.0.msi` (174,223,585 bytes; the payload is 171,324,553 bytes in 77
      files, so the MSI is uncompressed) and its `.msipkg.json` (`"compression": "None"`);
    - `git status --porcelain` was empty afterwards; only `packaging/windows/Enigma.Msi.Worker/.wixsharp/` appears,
      as ignored.
  - **The MSI's tables**, read with the vendored `WixToolset.Dtf.WindowsInstaller.dll`:
    - `ProductName` Enigma Launchpad, `Manufacturer` Josué Clément, `ProductVersion` 1.4.0 (the branch's version);
    - `UpgradeCode` `{B4C2D5A2-9182-4C17-B570-C6019EB5EC92}`, `ALLUSERS` 1, `ARPPRODUCTICON` `app_icon.ico`;
    - `INSTALLDIR` "Enigma Launchpad" under `ProgramFilesFolder`;
    - two "Enigma Launchpad.lnk" shortcuts (`DesktopFolder`, `ProgramMenuFolder`) on
      `[INSTALLDIR]\Enigma.Launchpad.Desktop.exe`, icon `IconFile1_app.ico`;
    - `Upgrade`: `VersionMax 1.4.0` (attributes 1) and `VersionMin 1.4.0` (attributes 2);
    - 77 files in one embedded cabinet; template `Intel;1033`.
    The same rows as the installed 1.4.0's package, except the product code.
- Fix budget: 0 cycles used.

## Documentation sweep

- `README.md`: a new *Windows installer* section after *Publish* (what `buildmsi` does, with the `git clean -dxf`
  warning; what the MSI installs; the prerequisites), and the layout table's `packaging/windows` row names the MSI
  scripts and the worker.
- `docs/RELEASE.md`:
  - §6 *Install it*: Windows now has an installer (it said "Windows and macOS have no installer"), with what it
    installs and what uninstalling it leaves;
  - §7: a Windows check after installing the MSI;
  - *Why there is no MSI profile* became *The Windows installer's identity*: the upgrade code is fixed in the script,
    the product code is new per build.
- `SECURITY.md`'s *Scope* lists "the Linux installer and the publish scripts in `packaging/`"; naming the Windows
  installer script there is the release's step (FEATURE-0859).
