# Ghost User Runner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a local-only Windows application that produces varied, human-like, safety-constrained browser and File Explorer activity for unlimited sessions.

**Architecture:** A .NET 8 WPF tray application owns a cancellable session state machine. A procedural planner proposes semantic actions, a default-deny policy approves them, and isolated Playwright, Windows UI Automation, and synthetic-input adapters execute them. Simulation mode exercises the complete planning and policy pipeline without touching the live desktop.

**Tech Stack:** C# 12, .NET 8 (`net8.0-windows`), WPF, xUnit, Microsoft.Playwright, FlaUI.UIA3, Win32 P/Invoke, Microsoft.Extensions.Hosting/Logging/Options, System.Text.Json, Inno Setup 6.

**Spec:** `docs/superpowers/specs/2026-09-22-ghost-user-runner-design.md`

## Global Constraints

- Target Windows desktop only; no service, server, account, telemetry, or deployment component.
- Use a dedicated Playwright browser profile with no personal credentials, cookies, extensions, or payment information.
- Default-deny every website, folder, extension, application, and semantic action not explicitly approved by validated configuration.
- Never capture physical keystrokes; inspect only the configured shutdown chord state.
- Never purchase, download, upload, authenticate, message, post, comment, submit forms, or mutate files.
- Stop on `Ctrl+Alt+Q` held for two seconds, the upper-left-corner gesture, tray Stop, policy failure, or cancellation.
- Every shutdown path must release synthetic keys/buttons and stop generating input.
- Sessions must support unlimited duration and pass a simulated 12-hour endurance test.
- The normal deliverable is a self-contained `win-x64` folder; an Inno Setup installer packages the same files.

## Review Focus

- Canonical paths containing junctions or `..` must not escape approved roots; Task 2 tests rejection.
- Redirects from an approved domain to an unapproved domain must be blocked before further interaction; Task 6 tests interception.
- Stop requests arriving while synthetic input is active must release all held input promptly; Task 5 tests cancellation cleanup.
- An unknown foreground window or dialog must stop input rather than receive a guessed click; Task 7 tests fail-closed recovery.
- Long sessions must not leak unbounded history or repeat an exact recent chain; Task 4 tests bounded history and cooldowns.

---

## File Map

```text
GhostUserRunner.sln
Directory.Build.props                         shared compiler and analyzer settings
src/GhostUserRunner.App/                      WPF composition root, tray UI, lifecycle
src/GhostUserRunner.Core/                     domain types, policy, planner, session state machine
src/GhostUserRunner.Infrastructure/           browser, desktop, input, hotkey, logs, configuration
tests/GhostUserRunner.Core.Tests/              deterministic policy/planner/session tests
tests/GhostUserRunner.Infrastructure.Tests/    adapter boundary and cancellation tests
config/appsettings.example.json               safe documented example configuration
installer/GhostUserRunner.iss                 optional local installer
scripts/publish.ps1                            reproducible portable build and installer entry point
```

Dependencies point inward: App -> Infrastructure -> Core. Core references no Windows automation or browser package.

### Task 1: Solution shell, configuration model, and simulation command

**Files:**
- Create: `GhostUserRunner.sln`
- Create: `Directory.Build.props`
- Create: `src/GhostUserRunner.Core/GhostUserRunner.Core.csproj`
- Create: `src/GhostUserRunner.Core/Configuration/RunnerOptions.cs`
- Create: `src/GhostUserRunner.Core/Configuration/OptionsValidator.cs`
- Create: `src/GhostUserRunner.Infrastructure/GhostUserRunner.Infrastructure.csproj`
- Create: `src/GhostUserRunner.App/GhostUserRunner.App.csproj`
- Create: `src/GhostUserRunner.App/App.xaml`
- Create: `src/GhostUserRunner.App/App.xaml.cs`
- Create: `tests/GhostUserRunner.Core.Tests/GhostUserRunner.Core.Tests.csproj`
- Create: `tests/GhostUserRunner.Infrastructure.Tests/GhostUserRunner.Infrastructure.Tests.csproj`
- Create: `tests/GhostUserRunner.Core.Tests/Configuration/OptionsValidatorTests.cs`
- Create: `config/appsettings.example.json`

**Interfaces:**
- Produces: `RunnerOptions`, `ValidationResult OptionsValidator.Validate(RunnerOptions)`, and `--simulate` process argument.

- [ ] **Step 1: Scaffold the solution and failing configuration tests**

Create the projects with `dotnet new`, add App -> Infrastructure -> Core references, add both test-project references, and add the package references named in the Tech Stack to the project that owns each integration. Test these exact cases:

```csharp
[Fact] public void RejectsEmptyAllowlists() =>
    Assert.False(OptionsValidator.Validate(new RunnerOptions()).IsValid);

[Fact] public void RejectsUnlimitedWildcardDomain() {
    var options = ValidOptions() with { AllowedDomains = ["*"] };
    Assert.Contains(OptionsValidator.Validate(options).Errors,
        e => e.Code == "domain.wildcard");
}

[Fact] public void AcceptsUnlimitedDuration() {
    var options = ValidOptions() with { SessionDuration = null };
    Assert.True(OptionsValidator.Validate(options).IsValid);
}
```

- [ ] **Step 2: Run tests and confirm the missing-type failure**

Run: `dotnet test tests/GhostUserRunner.Core.Tests -v normal`  
Expected: FAIL because `RunnerOptions` and `OptionsValidator` do not exist.

- [ ] **Step 3: Implement immutable validated options**

Define `RunnerOptions` with `AllowedDomains`, `AllowedFolderRoots`, `AllowedExtensions`, `AllowedApplications`, `ActivityWeights`, nullable `SessionDuration`, `RecentHistoryLimit`, and timing records. `OptionsValidator` must reject empty allowlists, wildcards, relative folder/executable paths, non-HTTPS domain entries, nonpositive weights, `RecentHistoryLimit` outside 10-10,000, and timing ranges where minimum exceeds maximum.

- [ ] **Step 4: Add safe example configuration and simulation startup branch**

The example must contain clearly marked sample local paths that fail with a clear message until the user replaces them, `youtube.com`, `www.youtube.com`, and a safe search provider. `App.xaml.cs` must validate configuration before constructing any adapter; `--simulate` must be parsed but may initially report that simulation is not implemented.

- [ ] **Step 5: Run the tests**

Run: `dotnet test GhostUserRunner.sln -v normal`  
Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add GhostUserRunner.sln Directory.Build.props src tests config
git commit -m "build: scaffold runner and validate configuration"
```

### Task 2: Default-deny safety policy

**Files:**
- Create: `src/GhostUserRunner.Core/Actions/ProposedAction.cs`
- Create: `src/GhostUserRunner.Core/Safety/ActionPolicy.cs`
- Create: `src/GhostUserRunner.Core/Safety/PolicyDecision.cs`
- Create: `src/GhostUserRunner.Infrastructure/Paths/CanonicalPathResolver.cs`
- Test: `tests/GhostUserRunner.Core.Tests/Safety/ActionPolicyTests.cs`
- Test: `tests/GhostUserRunner.Infrastructure.Tests/Paths/CanonicalPathResolverTests.cs`

**Interfaces:**
- Consumes: `RunnerOptions`.
- Produces: `PolicyDecision ActionPolicy.Evaluate(ProposedAction action, ObservedContext context)` and `string CanonicalPathResolver.Resolve(string path)`.

- [ ] **Step 1: Write table-driven failing policy tests**

```csharp
[Theory]
[InlineData(ActionKind.DeleteFile)]
[InlineData(ActionKind.SubmitForm)]
[InlineData(ActionKind.Download)]
[InlineData(ActionKind.SendMessage)]
public void AlwaysRejectsBlockedKinds(ActionKind kind) {
    Assert.False(_policy.Evaluate(new(kind, SafeTarget), SafeContext).Allowed);
}

[Fact] public void RejectsUnknownWindow() =>
    Assert.Equal("window.unknown", _policy.Evaluate(SafeAction,
        SafeContext with { IsRecognizedWindow = false }).ReasonCode);
```

Add path tests for sibling-prefix confusion (`C:\Safe2`), `..`, junction escape, missing paths, UNC paths, and an ordinary file beneath an approved root.

- [ ] **Step 2: Run focused tests and confirm failure**

Run: `dotnet test --filter "FullyQualifiedName~ActionPolicyTests|FullyQualifiedName~CanonicalPathResolverTests"`  
Expected: FAIL because policy types are missing.

- [ ] **Step 3: Implement semantic actions and policy**

Use a closed `ActionKind` enum. Permit only explicitly enumerated read-only actions. Normalize domains using `IdnHost.GetAscii`, compare exact host or configured subdomain boundaries, canonicalize filesystem targets, and require recognized process/window/target state. Return a reason code for every denial.

- [ ] **Step 4: Implement canonical path resolution**

Resolve full paths and reparse points, reject nonexistent targets, UNC/device paths, and targets not beneath an approved canonical root using separator-aware ordinal-ignore-case comparison.

- [ ] **Step 5: Run all tests and commit**

Run: `dotnet test GhostUserRunner.sln -v normal`  
Expected: PASS.

```powershell
git add src tests
git commit -m "feat: enforce default-deny action policy"
```

### Task 3: Cancellable session controller and structured action log

**Files:**
- Create: `src/GhostUserRunner.Core/Session/SessionController.cs`
- Create: `src/GhostUserRunner.Core/Session/SessionState.cs`
- Create: `src/GhostUserRunner.Core/Execution/IActionExecutor.cs`
- Create: `src/GhostUserRunner.Core/Planning/IActivityPlanner.cs`
- Create: `src/GhostUserRunner.Core/Logging/GeneratedActionEvent.cs`
- Test: `tests/GhostUserRunner.Core.Tests/Session/SessionControllerTests.cs`

**Interfaces:**
- Produces: `Task RunAsync(SessionRequest, CancellationToken)`, `Task PauseAsync()`, `void Resume()`, `Task StopAsync()`, and `IAsyncEnumerable<GeneratedActionEvent>`.

- [ ] **Step 1: Write failing state-machine tests**

Cover stopped -> running -> paused -> running -> stopping -> stopped, stop during execution, executor failure recovery, fatal policy rejection, and duplicate Start/Stop calls. Use `TaskCompletionSource` in a fake executor to prove `StopAsync` cancels in-flight work and waits for cleanup.

- [ ] **Step 2: Run and observe failure**

Run: `dotnet test --filter FullyQualifiedName~SessionControllerTests`  
Expected: FAIL because the controller is missing.

- [ ] **Step 3: Implement the controller**

Use one owned `CancellationTokenSource`, `SemaphoreSlim` for state transitions, and `ManualResetEventSlim` or an async equivalent for pause. The run loop requests one proposal, evaluates policy, executes, records the result, updates the planner, and repeats until duration/cancellation/fatal failure.

- [ ] **Step 4: Implement bounded structured logging**

`GeneratedActionEvent` may include timestamp, session ID, seed ID, action kind, generated query/approved URL/approved path, outcome, and reason code. It must not include clipboard, screenshots, credentials, or observed keystrokes. Configure rolling local JSON logs with bounded file count.

- [ ] **Step 5: Run tests and commit**

Run: `dotnet test GhostUserRunner.sln -v normal`  
Expected: PASS.

```powershell
git add src tests
git commit -m "feat: add cancellable session lifecycle"
```

### Task 4: Procedural planner, search topics, and simulation endurance

**Files:**
- Create: `src/GhostUserRunner.Core/Planning/ActivityPlanner.cs`
- Create: `src/GhostUserRunner.Core/Planning/ActivityChain.cs`
- Create: `src/GhostUserRunner.Core/Planning/BoundedHistory.cs`
- Create: `src/GhostUserRunner.Core/Randomness/IRandomSource.cs`
- Create: `src/GhostUserRunner.Core/Randomness/SeededRandomSource.cs`
- Create: `src/GhostUserRunner.Core/Topics/SearchTopicGenerator.cs`
- Create: `src/GhostUserRunner.Infrastructure/Simulation/SimulationExecutor.cs`
- Test: `tests/GhostUserRunner.Core.Tests/Planning/ActivityPlannerTests.cs`
- Test: `tests/GhostUserRunner.Core.Tests/Planning/EnduranceTests.cs`

**Interfaces:**
- Consumes: validated options and current `ObservedContext`.
- Produces: `ProposedAction Next(ObservedContext context)` and reproducible seeded streams.

- [ ] **Step 1: Write deterministic failing planner tests**

Assert that zero-weight categories are never selected, unavailable activities are excluded, exact recent chains respect cooldown, history never exceeds its configured bound, generated topics come only from configured safe categories, and the same seed yields the same first 1,000 proposals.

- [ ] **Step 2: Add the 12-hour simulated acceptance test**

Use a virtual clock and 100 representative seeds. Simulate 12 hours per seed, evaluate every proposal through `ActionPolicy`, assert zero denied executions, bounded memory/history, no exact chain repetition inside cooldown, and clean stop at the virtual deadline.

- [ ] **Step 3: Run and confirm missing implementation failure**

Run: `dotnet test --filter "FullyQualifiedName~ActivityPlannerTests|FullyQualifiedName~EnduranceTests"`  
Expected: FAIL.

- [ ] **Step 4: Implement weighted selection and bounded memory**

Use cumulative positive weights after eligibility and recent-history penalties. Build finite templates for browser search, YouTube viewing, file browsing, viewer use, window management, and idle. Generate fresh action parameters per chain; do not create arbitrary clicks or unrestricted text.

- [ ] **Step 5: Connect `--simulate` and run tests**

`--simulate --hours 12 --seed 12345` must print totals by action/outcome and exit nonzero on any policy denial or fatal failure.

Run: `dotnet run --project src/GhostUserRunner.App -- --simulate --hours 12 --seed 12345`  
Expected: exit 0 with zero unsafe actions.

- [ ] **Step 6: Commit**

```powershell
git add src tests
git commit -m "feat: add procedural planner and endurance simulation"
```

### Task 5: Human-like synthetic input and emergency stopping

**Files:**
- Create: `src/GhostUserRunner.Infrastructure/Input/HumanInputEngine.cs`
- Create: `src/GhostUserRunner.Infrastructure/Input/MousePathGenerator.cs`
- Create: `src/GhostUserRunner.Infrastructure/Input/Win32InputSink.cs`
- Create: `src/GhostUserRunner.Infrastructure/Safety/StopMonitor.cs`
- Test: `tests/GhostUserRunner.Infrastructure.Tests/Input/MousePathGeneratorTests.cs`
- Test: `tests/GhostUserRunner.Infrastructure.Tests/Input/HumanInputEngineTests.cs`
- Test: `tests/GhostUserRunner.Infrastructure.Tests/Safety/StopMonitorTests.cs`

**Interfaces:**
- Produces: cancellable `MoveAsync`, `ClickAsync`, `ScrollAsync`, `TypeAsync`; `StopMonitor.StopRequested`; and `IInputSink.ReleaseAll()`.

- [ ] **Step 1: Write failing input tests against a recording sink**

Test path endpoints/bounds, nonconstant timing, seed reproducibility, synthetic typo correction using only planner-owned text, cancellation between events, and unconditional `ReleaseAll` in `finally`. Test that only `Ctrl`, `Alt`, and `Q` key states are queried and that two seconds of continuous chord state requests stop.

- [ ] **Step 2: Write corner-failsafe tests**

Feed timestamped cursor positions and assert three entries into the upper-left 5x5-pixel region within five seconds requests stop; ordinary passes and stationary residence do not.

- [ ] **Step 3: Run and confirm failure**

Run: `dotnet test --filter "FullyQualifiedName~Input|FullyQualifiedName~StopMonitor"`  
Expected: FAIL.

- [ ] **Step 4: Implement input through `SendInput` and stop polling**

Keep Win32 calls behind `IInputSink`. Never install a general keyboard hook. Poll only configured chord keys with `GetAsyncKeyState`, poll pointer position for the corner gesture, and cancel the shared session token immediately on a confirmed stop.

- [ ] **Step 5: Run tests and commit**

Run: `dotnet test GhostUserRunner.sln -v normal`  
Expected: PASS.

```powershell
git add src tests
git commit -m "feat: add cancellable human input and stop controls"
```

### Task 6: Dedicated Playwright browser adapter

**Files:**
- Create: `src/GhostUserRunner.Infrastructure/Browser/BrowserAdapter.cs`
- Create: `src/GhostUserRunner.Infrastructure/Browser/BrowserPolicyInterceptor.cs`
- Create: `src/GhostUserRunner.Infrastructure/Browser/YouTubeOperations.cs`
- Test: `tests/GhostUserRunner.Infrastructure.Tests/Browser/BrowserPolicyInterceptorTests.cs`
- Test: `tests/GhostUserRunner.Infrastructure.Tests/Browser/BrowserAdapterTests.cs`

**Interfaces:**
- Implements: `IActionExecutor` for browser actions.
- Produces: recognized browser `ObservedContext` and structured outcomes.

- [ ] **Step 1: Write route-policy and fake-page tests**

Test exact/subdomain allowlisting, blocked redirects, downloads canceled through Playwright events, pop-ups closed unless their final URL is approved, login/CAPTCHA/form targets denied, and browser context created with a new dedicated data directory.

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test --filter FullyQualifiedName~Browser`  
Expected: FAIL.

- [ ] **Step 3: Implement browser lifecycle and interception**

Launch bundled Chromium headed, use a runner-owned profile directory, register routing and download handlers before navigation, verify the final URL after every navigation, and expose only semantic operations for search, approved result navigation, tabs, scrolling, and playback.

- [ ] **Step 4: Implement resilient YouTube operations**

Locate controls using accessible roles/names with bounded alternative selectors. Search, choose an ordinary video result, play/pause, seek, change volume, scroll, and exit. Reject sign-in, comment, share, upload, purchase, and consent flows rather than interacting with them.

- [ ] **Step 5: Run tests and a supervised smoke check**

Run: `pwsh src/GhostUserRunner.App/bin/Debug/net8.0-windows/playwright.ps1 install chromium`  
Run: `dotnet test GhostUserRunner.sln -v normal`  
Expected: PASS. Then run a five-minute browser-only session with test configuration and verify Stop works.

- [ ] **Step 6: Commit**

```powershell
git add src tests
git commit -m "feat: automate allowlisted browser activity"
```

### Task 7: File Explorer, viewers, and unknown-window recovery

**Files:**
- Create: `src/GhostUserRunner.Infrastructure/Desktop/DesktopContextReader.cs`
- Create: `src/GhostUserRunner.Infrastructure/Explorer/FileExplorerAdapter.cs`
- Create: `src/GhostUserRunner.Infrastructure/Viewers/ReadOnlyViewerAdapter.cs`
- Create: `src/GhostUserRunner.Infrastructure/Recovery/RecoveryCoordinator.cs`
- Test: `tests/GhostUserRunner.Infrastructure.Tests/Explorer/FileExplorerAdapterTests.cs`
- Test: `tests/GhostUserRunner.Infrastructure.Tests/Recovery/RecoveryCoordinatorTests.cs`

**Interfaces:**
- Implements: `IActionExecutor` for desktop/file actions.
- Produces: observed process, window, canonical path, recognized-state flag, and recovery outcomes.

- [ ] **Step 1: Write failing tests with fake UI Automation trees**

Test approved navigation, sibling/junction escape rejection, approved extension opening, blocked file commands, unknown foreground window, unexpected dialog, adapter timeout, and return to an approved root after invalid navigation.

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test --filter "FullyQualifiedName~Explorer|FullyQualifiedName~Recovery"`  
Expected: FAIL.

- [ ] **Step 3: Implement semantic File Explorer operations**

Use FlaUI UIA3 to open a configured root, navigate back/forward, select, scroll, change safe views, and open approved files. Re-read and canonicalize the address after navigation. Do not expose delete, rename, move, copy, paste, drag, context-menu, save, print, or share operations.

- [ ] **Step 4: Implement viewer and recovery boundaries**

Permit only configured executable-plus-extension pairs. Recognize the viewer window before interacting and allow only scroll, page navigation, zoom, minimize/restore, and close. On unknown focus/dialog, stop input; close only runner-owned known pop-ups, otherwise pause and return to a known-safe window or fail the session.

- [ ] **Step 5: Run tests and supervised smoke check**

Run: `dotnet test GhostUserRunner.sln -v normal`  
Expected: PASS. Run a five-minute test against a dedicated folder containing disposable sample TXT, PDF, and image files.

- [ ] **Step 6: Commit**

```powershell
git add src tests
git commit -m "feat: add read-only desktop and explorer activity"
```

### Task 8: Tray UI, composition, portable build, and installer

**Files:**
- Create: `src/GhostUserRunner.App/Tray/TrayController.cs`
- Create: `src/GhostUserRunner.App/MainWindow.xaml`
- Create: `src/GhostUserRunner.App/MainWindow.xaml.cs`
- Modify: `src/GhostUserRunner.App/App.xaml.cs`
- Create: `tests/GhostUserRunner.Core.Tests/Acceptance/CompositionTests.cs`
- Create: `scripts/publish.ps1`
- Create: `installer/GhostUserRunner.iss`
- Modify: `README.md`

**Interfaces:**
- Consumes: controller, planner, policy, adapters, logging, options, and stop monitor.
- Produces: visible local application and distributable artifacts.

- [ ] **Step 1: Write failing composition and UI-command tests**

Verify every supported `ActionKind` has exactly one executor, every executor is policy-gated, Start is disabled outside Stopped, Pause/Resume match state, Stop remains available while running/recovering, invalid configuration prevents Start, and app exit invokes `StopAsync`.

- [ ] **Step 2: Run and confirm failure**

Run: `dotnet test --filter "FullyQualifiedName~CompositionTests"`  
Expected: FAIL.

- [ ] **Step 3: Implement the visible tray application**

Show state, elapsed time, current activity, latest outcome, Start/Pause/Resume/Stop, configuration path, log path, stop chord, and corner-failsafe instructions. Keep the application visible in the tray; do not add hidden startup or persistence behavior.

- [ ] **Step 4: Wire production composition**

Register validated options, planner, policy, controller, adapters, stop monitor, and logging through `Microsoft.Extensions.Hosting`. Route every adapter invocation through the policy-gated composite executor. Treat unhandled background exceptions as fatal session failures followed by cleanup.

- [ ] **Step 5: Add reproducible portable packaging**

`scripts/publish.ps1` must run tests, install Playwright Chromium into the output, publish self-contained `win-x64`, copy example configuration without overwriting a user's existing configuration, calculate SHA-256 checksums, and place output under `artifacts/portable/GhostUserRunner`.

Run: `pwsh scripts/publish.ps1 -PortableOnly`  
Expected: a locally runnable `GhostUserRunner.App.exe` and Chromium assets.

- [ ] **Step 6: Add optional Inno Setup installer**

Package the exact portable output per-user, create Start Menu shortcuts, preserve user configuration on upgrade/uninstall, and provide no auto-start, scheduled task, service, network updater, or telemetry. `scripts/publish.ps1 -Installer` invokes `ISCC.exe` only when installed and otherwise prints the portable artifact location successfully.

- [ ] **Step 7: Perform final verification**

Run: `dotnet test GhostUserRunner.sln -c Release -v normal`  
Run: `dotnet run --project src/GhostUserRunner.App -c Release -- --simulate --hours 12 --seed 12345`  
Run: `pwsh scripts/publish.ps1 -PortableOnly`  
Expected: all tests pass, simulation reports zero unsafe actions, and the portable app starts locally.

Perform a supervised live soak in stages: 5 minutes browser-only, 5 minutes File Explorer-only, 30 minutes mixed, then 12 hours mixed. Exercise tray Stop, `Ctrl+Alt+Q`, and corner failsafe during active typing, mouse movement, navigation, playback, and idle.

- [ ] **Step 8: Update documentation and commit**

Document prerequisites, configuration, portable launch, optional installer build, safety limits, dedicated test profile/folder setup, and uninstall steps.

```powershell
git add src tests scripts installer README.md
git commit -m "feat: ship local Windows runner application"
```

## Completion Gate

Implementation is complete only when all automated tests pass, the 12-hour simulation records zero unsafe actions, all three stop mechanisms cleanly cancel active input, the portable artifact runs on the target PC, and the supervised staged soak succeeds. The optional installer may be omitted if the user chooses the portable build.
