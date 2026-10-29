# FEATURE-B7DD — Publish scripts for Windows and Linux

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-b7dd-publish-scripts`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

One-command publish scripts on both platforms, part of the uniform baseline of every Enigma app:

- Windows: new `publish.ps1` and `publish.bat`, both running
  `dotnet publish <Desktop project> -c Release -r win-x64 -p:PublishReadyToRun=true`;
- Linux: a new `publish.sh` next to `install.sh` / `uninstall.sh`, running
  `dotnet publish <Desktop project> -c Release -r linux-x64 -p:PublishReadyToRun=true`.

## Context & constraints

- No publish script exists. `packaging/linux/install.sh` and `uninstall.sh` exist (`set -euo pipefail`,
  `script_dir`, mode `100755` in the index). There is no `packaging/windows/`.
- GitClient's `packaging/windows/publish.ps1`, `publish.bat` and `packaging/linux/publish.sh` are the
  reference: the project is resolved from the script's own directory, and the script's exit code is
  `dotnet`'s.
- `.gitattributes` keeps `*.bat` CRLF and `*.sh` LF (to be checked; added if missing).
- `docs/RELEASE.md` §5 documents a self-contained `dotnet publish`. The README documents the Linux installer only.

## Steps

1. `packaging/windows/publish.ps1`: comment-based help, `Set-StrictMode`, `$ErrorActionPreference = 'Stop'`,
   the project resolved from `$PSScriptRoot`, the command, `exit $LASTEXITCODE`.
2. `packaging/windows/publish.bat`: `@echo off`, `setlocal`, the project resolved from `%~dp0`, the
   command, `exit /b %ERRORLEVEL%`. CRLF line endings.
3. `packaging/linux/publish.sh`: `#!/usr/bin/env bash`, `set -euo pipefail`, the project resolved from the
   script's directory, `exec` the command. Staged with mode `100755`.
4. A Desktop test (`PackagingScriptsTests`): every publish script names `src/Enigma.Launchpad.Desktop`, its
   runtime identifier, `-c Release` and `-p:PublishReadyToRun=true`.
5. `docs/RELEASE.md` §5 and the README: name the three scripts and where their output lands.
6. Verify: run `publish.bat` and `publish.ps1` (win-x64), and `publish.sh` through Git Bash (linux-x64).
   Each exits 0 and leaves its `publish/` folder.

## Acceptance criteria

- The three scripts run from any directory and exit with `dotnet`'s exit code.
- `publish.sh` is executable in the index (`100755`); `publish.bat` is CRLF in the working tree.
- The Release build has zero warnings; the whole suite is green.

## Out of scope

- Self-contained publishing, other runtime identifiers, an archive (`install.sh` covers those on Linux).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Where | `packaging/windows/`, `packaging/linux/` | Next to the existing scripts of each platform, as GitClient | A root `publish/` folder |
| `publish.bat` | Calls `dotnet` itself | No dependency on PowerShell's execution policy | Wrapping `publish.ps1` |
| Arguments | None accepted | The spec fixes the command; one obvious behaviour | Passing `%*` / `"$@"` through |
| Linux RID | `linux-x64`, fixed | The spec's command; `install.sh --rid` covers arm64 | Guessing from `uname -m` |
| Self-contained | Not set (framework-dependent, the .NET 10 default for a RID publish) | The spec's command, verbatim | Adding `--self-contained` |
| Tests | A content test + running all three scripts | Pins the command against drift; running them is the real proof | No test |
