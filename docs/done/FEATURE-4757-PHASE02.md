# FEATURE-4757-PHASE02 — Process-wide exception hooks

## Summary

Every exception nothing else handles now reaches the safety net from PHASE01:

- `UnhandledExceptionHooks` (ported from GitClient):
  - `Install()`, the first line of `Main`, hooks `AppDomain.UnhandledException` and
    `TaskScheduler.UnobservedTaskException` (marked observed);
  - `InstallDispatcher()`, the first thing `App` does on the desktop lifetime, hooks
    `Dispatcher.UIThread.UnhandledException` (marked handled, so a throwing command or `async void` handler no
    longer ends the process);
  - reports made before the net exists are kept (at most 32) and traced, then handed over on `Attach`.
- `App` attaches the reporter as soon as the host is built, and tells it at shutdown (`ShutdownRequested`,
  `OnExit`). `OnExit` detaches it before the container is disposed.
- `Task.Forget()` (a C# 14 extension, as in GitClient) starts work nobody awaits and reports a fault as
  `FireAndForget`. It replaces the three `_ = task` discards: two in `ShortcutViewModel`, one in
  `InfoBarNotifications`.

## Files/modules touched

- Created: `src/Enigma.Launchpad.Desktop/Services/UnhandledExceptionHooks.cs`, `Services/TaskExtensions.cs`
- Modified: `src/Enigma.Launchpad.Desktop/Program.cs`, `App.axaml.cs`, `ViewModels/Pages/ShortcutViewModel.cs`,
  `Services/UserNotifications.cs`
- Created: `tests/Enigma.Launchpad.Desktop.UnitTests/UnhandledExceptionHooksTests.cs`
- Modified: `docs/roadmap.md`, `docs/plan/FEATURE-4757.md`; created `docs/done/FEATURE-4757-PHASE02.md`

## Deviations & follow-ups

- **Startup failures.** The plan said a startup failure after the host exists is "reported (`Startup`)".
  Launchpad's startup steps (settings, hosts, launchpad, main ViewModel) are not wrapped:
  - carrying on after a failed launchpad load could save an empty launchpad over the user's file;
  - such a failure escapes `Main`, and the `AppDomain` hook logs it through the attached reporter before the
    process ends;
  - `UnhandledErrorSource.Startup` stays in the shared vocabulary.
- **`UserNotifications`.** Its `_ = _infoBar.ShowAsync(...)` also moved to `Forget()`, beyond the plan's
  `ShortcutViewModel`. It is the same unawaited-work pattern the spec's "catch all unhandled exceptions" covers.
- **To verify by hand.** The dispatcher hook is not exercised by a test: installing it in the test process
  would mark every later headless test's dispatcher exception handled and hide its failure. `App`'s desktop
  startup is not reached by the headless tests either. Suggested manual check: start the app, confirm it
  opens, close it.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx -c Release`: 0 warnings, 0 errors. Debug: 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx -c Release`: 1025 total, 1024 passed, 0 failed, 1 skipped
  (Unix-only). Debug: the same counts.
- `UnhandledExceptionHooksTests` (7, in a `process-wide` collection that runs alone):
  - early reports are handed over in order;
  - a report reaches an attached net at once;
  - after `Detach`, reports wait for the next net;
  - `Forget` reports a failed task;
  - `Forget` reports a task that fails later, one report per error;
  - `Forget` says nothing for a success or a cancellation;
  - `Install` hands an unobserved fault to the net (found through GC finalisation, 3/3 repeated runs green).
- Fix cycle 1 of 3: `xUnit1051` refused `TaskCompletionSource.SetCanceled()` without a token. The test passes
  an already-cancelled token, which the cancelled task should carry anyway.

## Documentation sweep

- Nothing stale: no document describes how errors are handled.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| The dispatcher hook in tests | Not installed | It is process-wide and would hide later tests' failures |
| Test isolation | A `DisableParallelization` collection | The hooks are static; parallel tests could report into another test's net |
| Wrapping startup steps | No | Keeping a window up after a failed load risks overwriting the user's data |
