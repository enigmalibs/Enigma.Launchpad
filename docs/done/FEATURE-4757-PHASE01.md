# FEATURE-4757-PHASE01 — Error policy and dialog

## Summary

The safety net's decision logic and its dialog, ported from Enigma.GitClient and adapted to Launchpad's single
window:

- `UnhandledErrorPolicy` decides what each exception does:
  - **Ignore** a cancellation someone asked for, or an exception already offered;
  - **Merge** an error into the dialog waiting or on screen;
  - **Suppress** an error closed less than a minute ago;
  - **Queue** anything else. It also builds the one-line message and the details: type, message and stack,
    inner exceptions included, never `Exception.Data` or `ToString()`.
- `UnhandledErrorReporter` (`IUnhandledErrorReporter`) logs every report at once (Error, or Debug for an ignored
  cancellation), then shows "Something went wrong" on the main window's dialog host:
  - the Warning glyph in `#E8A33D`, the message, "N more errors occurred", the details in an expander with
    Copy details, and one Close button;
  - it waits for a busy host, and before the host, until `RegisterHost`;
  - a dialog that fails to show is logged once and turns the net off.
- `App.RegisterHosts` hands the reporter the window's `HostDialog`.

PHASE02 hooks the process-wide events into `Report`.

## Files/modules touched

- Created: `src/Enigma.Launchpad.Desktop/Services/UnhandledErrorPolicy.cs`, `Services/UnhandledErrorReporter.cs`,
  `ViewModels/Dialogs/UnhandledErrorViewModel.cs`, `Views/Dialogs/UnhandledErrorView.axaml(.cs)`
- Modified: `src/Enigma.Launchpad.Desktop/App.axaml.cs`, `DependencyInjection/ServiceCollectionExtensions.cs`
- Created: `tests/Enigma.Launchpad.Desktop.UnitTests/UnhandledErrorPolicyTests.cs`, `UnhandledErrorReporterTests.cs`,
  `Infrastructure/ManualTimeProvider.cs`
- Modified: `tests/Enigma.Launchpad.Desktop.UnitTests/CompositionRootTests.cs`
- Modified: `docs/roadmap.md`, `docs/plan/FEATURE-4757.md`; created `docs/done/FEATURE-4757-PHASE01.md`

## Deviations & follow-ups

- `TimeProvider` was not registered in this dev: `AddLaunchpadCore` already does it (`TryAddSingleton`).
- Dropped from the view: GitClient's remedy line (token recovery) and its `toolbar` button class (Launchpad has
  none). The monospace font list is inline, since Launchpad has no `MonospaceFontFamily` resource.
- No `ArgumentRedactor`: Launchpad holds no credentials. The details still leave out `Data` and `ToString()`,
  pinned by a test.
- Unawaited calls to `PresentAsync` use `_ =`, which is safe because it never throws. PHASE02's `Forget()` is
  for work that can fault.

## Build/test evidence

- `dotnet build Enigma.Launchpad.slnx -c Release`: 0 warnings, 0 errors.
- `dotnet test --solution Enigma.Launchpad.slnx -c Release`: 1018 total, 1017 passed, 0 failed, 1 skipped
  (Unix-only).
- Tests added:
  - `UnhandledErrorPolicyTests` (14): queue, merge while waiting and while on screen, the same exception once,
    cancellations asked for and not, shutting down, the repeat window on both sides, summarise unwrapping,
    details with inner exceptions and stacks and without `Data`, aggregates numbered, batches numbered;
  - `UnhandledErrorReporterTests` (6, headless, on the real main window):
    - logged and shown with title, icon, message and one Close button;
    - a second error counted in;
    - a requested cancellation shows nothing and logs at Debug;
    - an error waits for an open question;
    - an error before the host is shown once it registers;
    - Copy details reaches the clipboard;
  - `CompositionRootTests` resolves `IUnhandledErrorReporter`.

## Documentation sweep

- Nothing stale: no document describes how errors are handled.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| `RegisterHost` on the reporter's interface | Yes | Launchpad hands hosts over from `App.RegisterHosts`; no window abstraction to port |
| `Title` as a public constant | Yes | Tests assert it without repeating the sentence |
| Copy button style | The default button | The view is GitClient's; Launchpad's `ghost` class would hide the button's outline in a dialog |
