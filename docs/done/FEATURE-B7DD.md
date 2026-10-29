# FEATURE-B7DD — Publish scripts for Windows and Linux

## Summary

There are now one-command publish scripts, in GitClient's shape:

- `packaging/windows/publish.ps1` and `publish.bat` run
  `dotnet publish <src/Enigma.Launchpad.Desktop> -c Release -r win-x64 -p:PublishReadyToRun=true`;
- `packaging/linux/publish.sh` runs the same with `-r linux-x64`.

Each resolves the project from its own directory, so it runs from anywhere, and exits with `dotnet`'s exit
code. A content test pins the project and the command. The README and the release runbook name the scripts.

## Files/modules touched

- Created: `packaging/windows/publish.ps1`, `packaging/windows/publish.bat`, `packaging/linux/publish.sh`
- Created: `tests/Enigma.Launchpad.Desktop.UnitTests/PackagingScriptsTests.cs`,
  `tests/Enigma.Launchpad.Desktop.UnitTests/Infrastructure/RepositoryRoot.cs`
- Modified: `tests/Enigma.Launchpad.Desktop.UnitTests/AppIconTests.cs` (uses `RepositoryRoot`)
- Modified: `README.md`, `docs/RELEASE.md`
- Modified: `docs/roadmap.md`, `docs/plan/FEATURE-B7DD.md`; created `docs/done/FEATURE-B7DD.md`

## Deviations & follow-ups

- The plan said to extend an `AssemblyNameConsistencyTests`, as GitClient did. Launchpad has none, so the
  test is a new `PackagingScriptsTests` that also checks the named project exists.
- `.gitattributes` already had `*.bat eol=crlf` and `*.sh eol=lf`. Nothing was added.
- The repository-root lookup moved from `AppIconTests` into `Infrastructure/RepositoryRoot`, now that two
  test classes need it.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx -c Release`: 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx -c Release`: 972 total, 971 passed, 0 failed, 1 skipped
  (Unix-only). 3 tests added: `ThePublishScripts_PublishTheDesktopProjectInReleaseReadyToRun`, once per script.
- Each script was run from `%TEMP%` / `$HOME`, not the repository:
  - `publish.bat`: exit 0, `win-x64/publish/` with `Enigma.Launchpad.Desktop.exe` and `app.ico`;
  - `publish.ps1` (`powershell -ExecutionPolicy Bypass -File`): exit 0, the same folder;
  - `publish.sh` (Git Bash): exit 0, `linux-x64/publish/` with `Enigma.Launchpad.Desktop` and `app.ico`.
- `publish.bat` is CRLF in the working tree. `publish.sh` is staged with mode `100755`.

## Documentation sweep

- `README.md`: a new *Publish* section (the scripts and where their output lands). The layout table names
  `packaging/windows` and the Linux publish script.
- `docs/RELEASE.md` §5: the three scripts and their output. §6: Windows publishes with the scripts.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Test home | A new `PackagingScriptsTests` | There is no assembly-name consistency test to extend |
| `RepositoryRoot` helper | Extracted to `Infrastructure/` | Two test classes need it; duplicating the loop would drift |
| README placement | A *Publish* section before *Install on Linux* | Publishing comes before installing, as in GitClient's README |
