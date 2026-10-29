# FEATURE-4757 — Safety net for unhandled errors

**Status:** DONE
**Type:** FEATURE
**Branch:** `feature/feature-4757-phase01-error-dialog`, `feature/feature-4757-phase02-exception-hooks`
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

Every exception that nothing else handles is caught, logged, and shown in an error content dialog: the
application is still running, what went wrong, how many more errors came, and the details with a Copy
button. This is the uniform safety net of the Enigma apps, ported from Enigma.GitClient.

## Context & constraints

- **Launchpad today** has no unhandled-exception hook. `Program.Main` calls
  `StartWithClassicDesktopLifetime` directly. A throwing command or `async void` handler reaches Avalonia's
  dispatcher, which ends the process. `_ = LoadTargetIconAsync()` (ShortcutViewModel) is fire-and-forget.
- **The reference** is GitClient (`develop` @ 2c781d3), `Services/`:
  - `UnhandledErrorPolicy`: Ignore / Suppress / Queue / Merge, a one-minute repeat window, batches,
    `Summarise`, `BuildDetails`;
  - `UnhandledErrorReporter`: logs on the reporting thread, decides on the UI thread, one dialog at a time,
    waits for a busy host;
  - `UnhandledExceptionHooks`: AppDomain, TaskScheduler and Dispatcher hooks, an early queue, Attach/Detach;
  - `UnhandledErrorViewModel` / `UnhandledErrorView`: a Warning icon `#E8A33D`, "Something went wrong",
    Close, the details in an expander.
- **What GitClient has that Launchpad does not:**
  - several windows (`IAppWindows`, `IHostWindow`);
  - token recovery;
  - credentials in messages (`ArgumentRedactor`).
  Launchpad has one window whose `HostDialog` `App.RegisterHosts` hands to the services.
- `IClipboardService` puts text on the clipboard. `TimeProvider` is not registered yet.
- `ContentDialog` (Enigma.Avalonia.Desktop) exposes `IsOpen` and a `Closed` event. `IContentDialogService`
  resets the host before each show.

## PHASE01 — Error policy and dialog

**Status:** DONE

Steps:
1. `Services/UnhandledErrorPolicy.cs`: `UnhandledErrorSource`, `UnhandledErrorDecision`,
   `UnhandledErrorBatch` and `UnhandledErrorPolicy`, ported without the redactor. The details hold the type,
   message and stack of each exception and its inner exceptions only, never `Exception.Data` or `ToString()`.
2. `ViewModels/Dialogs/UnhandledErrorViewModel` (`ObservableObject`, `IClipboardService`) and
   `Views/Dialogs/UnhandledErrorView.axaml(.cs)`, GitClient's markup without the remedy line.
3. `Services/UnhandledErrorReporter.cs` (`IUnhandledErrorReporter`):
   - `Report(exception, source)`: logs at once (Debug for an ignored cancellation, Error otherwise), then posts
     the offer to the UI thread;
   - `RegisterHost(ContentDialog)`: the main window's dialog host. Errors before it wait for it;
   - a busy host is waited for (`Closed`), and the dialog is shown through `IContentDialogService`;
   - a dialog that fails to show is logged once and turns the net off (no loop);
   - `ShutdownStarted()`.
4. DI: `TimeProvider.System` (via `TryAddSingleton`) and `IUnhandledErrorReporter` as singletons in
   `AddLaunchpadApp`. `App.RegisterHosts` registers the reporter's host.
5. Tests:
   - `UnhandledErrorPolicyTests`: ignore, merge, suppress within and after the window, summarise unwraps,
     details carry inner exceptions and no `Data`;
   - `UnhandledErrorReporterTests` (headless, with a recording dialog service): shows the dialog after the
     host registers, merges a second error, waits for a busy host, logs every error;
   - `CompositionRootTests` resolves `IUnhandledErrorReporter`.

Acceptance criteria:
- A reported exception logs at Error and opens "Something went wrong" with a Warning icon, the message,
  the details and Copy details. A second one while it is open shows "1 more error occurred."
- The same error reported again within a minute of closing is logged, not shown.
- The whole suite is green; the Release build has zero warnings.

## PHASE02 — Process-wide exception hooks

**Status:** DONE

Steps:
1. `Services/UnhandledExceptionHooks.cs`, ported:
   - `Install` (AppDomain + `TaskScheduler.UnobservedTaskException`, `SetObserved`);
   - `InstallDispatcher` (`Dispatcher.UIThread.UnhandledException`, `Handled = true`);
   - `Attach` / `Detach` / `Report`, with a bounded early queue traced until attached.
2. `Program.Main`: `UnhandledExceptionHooks.Install()` first.
3. `App.OnFrameworkInitializationCompleted`:
   - `InstallDispatcher()` first;
   - `Attach(reporter)` once the host is built;
   - a startup failure after the host exists is reported (`Startup`) rather than lost.
4. `App.OnExit`: `ShutdownStarted()`, then `Detach()` before the host is disposed.
5. `TaskExtensions.Forget()` (reports through the hooks with `FireAndForget`); `ShortcutViewModel` uses it
   for `LoadTargetIconAsync`.
6. Tests (`UnhandledExceptionHooksTests`, headless):
   - early reports are handed over on `Attach`;
   - a throwing dispatcher callback reaches the reporter and is marked handled;
   - an unobserved task exception is reported;
   - `Forget` reports a fault and ignores success.

Acceptance criteria:
- A command that throws on the UI thread shows the dialog, and the application keeps running.
- A faulted fire-and-forget task shows the dialog.
- The whole suite is green; the Release build has zero warnings.

## Out of scope

- Writing logs to a file (the host's default providers stay).
- Restarting the application, or sending reports anywhere.
- Changing the errors the application already handles in its own words (info bars, dialogs).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| How much of GitClient's net | Policy, reporter, hooks and view, adapted to one window | The uniform baseline; batching and the repeat window stop a dialog storm | A bare `try/catch` around `Main`; a message box per exception |
| Host discovery | `RegisterHost(ContentDialog)` called by `App.RegisterHosts` | Launchpad has one window and already hands hosts over this way | Porting `IAppWindows` |
| Redaction | None | Launchpad holds no credentials; details never include `Data`/`ToString()` | Porting `ArgumentRedactor` |
| Token recovery / remedy line | Dropped | GitClient-specific | Keeping an empty remedy slot |
| AppDomain exceptions | Logged only (the process ends) | .NET ends the process after the event whatever a handler does | Trying to keep the process alive |
| Phasing | Dialog first, hooks second | Each phase is one reviewable commit; PHASE01 is usable through `Report` and tested on its own | One phase |
| Fire-and-forget | A `Forget()` extension, used by `ShortcutViewModel` | A discarded task's fault otherwise surfaces only at finalisation, if ever | Leaving `_ =` |
