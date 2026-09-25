# Ghost User Runner Web Control Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the WPF/tray controls with a loopback-only web dashboard that starts and manages randomized, real-input Windows browser simulations lasting at least eight hours with no generated pause longer than 40 seconds.

**Architecture:** Keep the Core planner, policy, controller, Infrastructure adapters, and Win32 input engine, then host them from an ASP.NET Core Windows executable bound to loopback. Playwright locates and validates browser targets; verified screen coordinates are handed to Win32 input for visible movement, clicks, scrolling, and typing. A single-session service separates HTTP request lifetimes from the long-running controller and publishes immutable status snapshots to the dashboard.

**Tech Stack:** .NET 8 for Windows, ASP.NET Core minimal APIs, static HTML/CSS/JavaScript, Microsoft Playwright, Win32 `SendInput`, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-24-web-runner-design.md`

## Global Constraints

- The web server binds only to `127.0.0.1` and `::1`; never use a wildcard or LAN binding.
- A normal session defaults to eight hours and rejects any duration shorter than eight hours.
- Every generated running-state pause, dwell, hesitation, and recovery delay is at most 40 seconds; an explicit user pause is exempt.
- Only one session and one dedicated Chromium profile may be active.
- Real input is emitted only after the owned browser window and semantic target are verified.
- Existing default-deny restrictions and `Ctrl+Alt+Q` emergency shutdown remain active.
- Every pause, stop, failure, and process-exit path releases held keys and mouse buttons.
- The project directory is not currently a Git repository. Do not initialize one implicitly; replace each commit checkpoint below with a commit only if version control has been initialized by the user.

## Review Focus

- Concurrent Run requests must create exactly one session and return HTTP 409 for the loser; Task 4 pins this with a barrier-based service test.
- A dashboard refresh or closed tab must not cancel a running session; Task 4 starts with a request token, cancels it, and verifies the owned session token remains live.
- Durations that are missing, negative, enormous, or below eight hours must resolve safely; Tasks 1 and 5 cover model and HTTP validation.
- Pause racing with Stop must end stopped and release input exactly once without deadlock; Tasks 2 and 3 cover the race and cleanup contract.
- A browser element that moved, disappeared, or lost focus between discovery and input must receive no real click or keystroke; Task 3 revalidates immediately before each Win32 event.

---

### Task 1: Session Contract, Duration Rules, and Timing Cap

**Files:**
- Modify: `src/GhostUserRunner.Core/Session/SessionState.cs`
- Modify: `src/GhostUserRunner.Core/Configuration/RunnerOptions.cs`
- Modify: `src/GhostUserRunner.Core/Configuration/OptionsValidator.cs`
- Modify: `src/GhostUserRunner.Core/Planning/ActivityDwellTime.cs`
- Modify: `tests/GhostUserRunner.Core.Tests/Configuration/OptionsValidatorTests.cs`
- Modify: `tests/GhostUserRunner.Core.Tests/Planning/ActivityPlannerTests.cs`

**Interfaces:**
- Produces: `SessionLimits.MinimumDuration`, `SessionLimits.DefaultDuration`, and `SessionLimits.MaximumGeneratedDelay` as `TimeSpan` constants exposed as static properties.
- Produces: `SessionState` values `Stopped`, `Starting`, `Running`, `Pausing`, `Paused`, `Stopping`, `Completed`, and `Failed`.
- Produces: `SessionRequest(TimeSpan Duration, int Seed)` with a non-null duration.
- Consumes: existing `RunnerOptions.SessionDuration` and timing ranges loaded from JSON.

- [ ] **Step 1: Write failing duration and timing tests**

Add tests that require the validator to reject a configured duration under eight hours and a pause maximum over 40 seconds, accept exactly eight hours, and require every dwell category to remain at or below 40 seconds:

```csharp
[Theory]
[InlineData(7, 59)]
[InlineData(-1, 0)]
public void RejectsSessionDurationBelowEightHours(int hours, int minutes)
{
    var result = OptionsValidator.Validate(ValidOptions() with
    {
        SessionDuration = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes)
    });
    Assert.Contains(result.Errors, error => error.Code == "session.too_short");
}

[Fact]
public void AcceptsEightHourSessionAndFortySecondPauseMaximum()
{
    var options = ValidOptions() with
    {
        SessionDuration = TimeSpan.FromHours(8),
        Timing = ValidOptions().Timing with { PauseMilliseconds = new IntRange(250, 40_000) }
    };
    Assert.True(OptionsValidator.Validate(options).IsValid);
}

[Fact]
public void RejectsPauseMaximumAboveFortySeconds()
{
    var options = ValidOptions() with
    {
        Timing = ValidOptions().Timing with { PauseMilliseconds = new IntRange(250, 40_001) }
    };
    Assert.Contains(OptionsValidator.Validate(options).Errors,
        error => error.Code == "timing.pause_too_long");
}

[Theory]
[InlineData(ActionKind.WatchVideo)]
[InlineData(ActionKind.SearchWeb)]
[InlineData(ActionKind.Idle)]
[InlineData(ActionKind.NavigateWeb)]
public void EveryGeneratedDwellIsAtMostFortySeconds(ActionKind kind)
{
    for (var seed = 0; seed < 1_000; seed++)
        Assert.InRange(ActivityDwellTime.Choose(kind, new SeededRandomSource(seed)),
            TimeSpan.Zero, SessionLimits.MaximumGeneratedDelay);
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\GhostUserRunner.Core.Tests\GhostUserRunner.Core.Tests.csproj --filter "FullyQualifiedName~OptionsValidatorTests|FullyQualifiedName~ActivityPlannerTests"
```

Expected: FAIL because `SessionLimits` does not exist, short durations are accepted, and video dwell can reach 180 seconds.

- [ ] **Step 3: Add the shared limits and enforce them**

Create `SessionLimits` in `SessionState.cs`, expand `SessionState`, make `SessionRequest.Duration` non-null, add validator errors, and cap dwell selection before constructing the `TimeSpan`:

```csharp
public static class SessionLimits
{
    public static TimeSpan MinimumDuration => TimeSpan.FromHours(8);
    public static TimeSpan DefaultDuration => TimeSpan.FromHours(8);
    public static TimeSpan MaximumGeneratedDelay => TimeSpan.FromSeconds(40);
}

public enum SessionState
{
    Stopped, Starting, Running, Pausing, Paused, Stopping, Completed, Failed
}

public sealed record SessionRequest(TimeSpan Duration, int Seed);
```

In `OptionsValidator.Validate`, add `session.too_short` when a non-null configured duration is below the minimum and `timing.pause_too_long` when `PauseMilliseconds.Maximum > 40_000`. Keep `null` valid because it means “use the eight-hour default,” not unlimited execution. In `ActivityDwellTime.Choose`, change the `WatchVideo` range to `(30, 40)` and clamp all selected values to `SessionLimits.MaximumGeneratedDelay`.

- [ ] **Step 4: Run the focused tests and the full Core suite**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\GhostUserRunner.Core.Tests\GhostUserRunner.Core.Tests.csproj
```

Expected: PASS. Update the former `AcceptsUnlimitedDuration` test name and assertion to state that null selects the default at composition time.

- [ ] **Step 5: Record the checkpoint**

If Git has been initialized:

```powershell
git add src/GhostUserRunner.Core tests/GhostUserRunner.Core.Tests
git commit -m "feat: enforce web runner session limits"
```

Otherwise check Task 1 in this plan and continue without initializing Git.

### Task 2: Cancellation-Safe Session Controller and Status Snapshots

**Files:**
- Create: `src/GhostUserRunner.Core/Session/SessionStatus.cs`
- Modify: `src/GhostUserRunner.Core/Session/SessionController.cs`
- Modify: `tests/GhostUserRunner.Core.Tests/Session/SessionControllerTests.cs`

**Interfaces:**
- Consumes: `SessionRequest(TimeSpan Duration, int Seed)` and expanded `SessionState` from Task 1.
- Produces: `SessionStatus(SessionState State, Guid? SessionId, DateTimeOffset? StartedAt, TimeSpan Elapsed, TimeSpan? Remaining, string? CurrentActivity, GeneratedActionEvent? LastAction, IReadOnlyList<GeneratedActionEvent> RecentEvents, string? Error)`.
- Produces: `SessionController.GetStatus(DateTimeOffset now)` returning an immutable copy.
- Produces: existing `RunAsync`, `PauseAsync`, `Resume`, and `StopAsync` with cancellation-safe semantics.

- [ ] **Step 1: Add failing lifecycle and race tests**

Add tests for duplicate starts, natural completion, pause cancellation, Stop racing with Pause, bounded event snapshots, and status timing. Use a `ControllableExecutor` with a per-execution `TaskCompletionSource`, a cancellation counter, and a `Release` method. The core race assertion is:

```csharp
[Fact]
public async Task PauseRacingWithStopEndsStoppedWithoutDeadlock()
{
    var executor = new ControllableExecutor();
    var controller = CreateController(executor);
    var run = controller.RunAsync(new(TimeSpan.FromHours(8), 10), CancellationToken.None);
    await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

    var pause = controller.PauseAsync();
    var stop = controller.StopAsync();
    await Task.WhenAll(pause, stop).WaitAsync(TimeSpan.FromSeconds(2));
    await run;

    Assert.Equal(SessionState.Stopped, controller.State);
    Assert.Equal(1, executor.CancellationCount);
}
```

For natural completion, inject a fake clock or an internal duration-cancellation factory so the test does not wait eight hours. Grant test access with `InternalsVisibleTo` if needed.

- [ ] **Step 2: Run the controller tests and verify failure**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\GhostUserRunner.Core.Tests\GhostUserRunner.Core.Tests.csproj --filter FullyQualifiedName~SessionControllerTests
```

Expected: FAIL because pause does not cancel in-flight input, the new states/status do not exist, and lifecycle transitions are not serialized.

- [ ] **Step 3: Implement serialized state transitions and per-action cancellation**

Refactor the controller so `_transition` guards every command; the session has a lifetime cancellation source and each action has a linked cancellation source. `PauseAsync` transitions `Running -> Pausing`, cancels and awaits the current action, then transitions to `Paused`. `Resume` transitions `Paused -> Running` and pulses an async resume signal. `StopAsync` cancels lifetime and action tokens, awaits the run task, and ends `Stopped`. Natural duration expiration ends `Completed`; policy or unhandled execution failure ends `Failed` with an error string.

Copy event data under a lock and keep only the most recent 100 events in `SessionStatus`. Calculate remaining time as `max(TimeSpan.Zero, Duration - Elapsed)`. Do not hold `_transition` while awaiting the full eight-hour run task.

- [ ] **Step 4: Run controller tests and Core regression suite**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\GhostUserRunner.Core.Tests\GhostUserRunner.Core.Tests.csproj
```

Expected: PASS with no test taking longer than two seconds.

- [ ] **Step 5: Record the checkpoint**

If Git exists, commit `feat: make session lifecycle web-safe`; otherwise mark Task 2 complete in this plan.

### Task 3: Verified Real Windows Clicks, Scrolling, and Keystrokes

**Files:**
- Modify: `src/GhostUserRunner.Infrastructure/Input/HumanInputEngine.cs`
- Modify: `src/GhostUserRunner.Infrastructure/Input/Win32InputSink.cs`
- Create: `src/GhostUserRunner.Infrastructure/Browser/BrowserScreenTarget.cs`
- Modify: `src/GhostUserRunner.Infrastructure/Browser/BrowserAdapter.cs`
- Modify: `tests/GhostUserRunner.Infrastructure.Tests/Input/HumanInputEngineTests.cs`
- Modify: `tests/GhostUserRunner.Infrastructure.Tests/Browser/BrowserPolicyInterceptorTests.cs`
- Create: `tests/GhostUserRunner.Infrastructure.Tests/Browser/BrowserAdapterInputTests.cs`

**Interfaces:**
- Produces: `IInputSink.MouseButtonDown()`, existing `MouseButtonUp()`, and `HumanInputEngine.ClickAsync`, `ScrollAsync`, and `TypeTextAsync`.
- Produces: `IBrowserTargetVerifier.ResolveAsync(IPage page, ILocator locator, CancellationToken token)` returning `BrowserScreenTarget?` and `RevalidateAsync(BrowserScreenTarget target, CancellationToken token)` returning `bool`.
- Consumes: owned browser process/window focus and Playwright locator bounding boxes.

- [ ] **Step 1: Add failing input-engine tests**

Extend the recording input sink and assert exact down/up balance, scrolling, text conversion including uppercase and spaces, cancellation cleanup, and no click after failed revalidation:

```csharp
[Fact]
public async Task ClickAlwaysBalancesMouseDownAndUp()
{
    var sink = new RecordingInputSink();
    var engine = new HumanInputEngine(sink, new SeededRandomSource(1));
    await engine.ClickAsync(new Point(120, 80), CancellationToken.None);
    Assert.Equal(new[] { "move:120,80", "mouse-down", "mouse-up" }, sink.Events);
}

[Fact]
public async Task CancelledTypingReleasesAllInput()
{
    var sink = new CancellingInputSink(cancelAfterKeyDown: true);
    var engine = new HumanInputEngine(sink, new SeededRandomSource(2));
    await Assert.ThrowsAnyAsync<OperationCanceledException>(
        () => engine.TypeTextAsync("hello", sink.Token));
    Assert.True(sink.ReleaseAllCalled);
}
```

Add adapter tests with mocked verifier/focus/input abstractions showing that a vanished target results in `browser.target_changed` and zero Win32 events.

- [ ] **Step 2: Run Infrastructure input/browser tests and verify failure**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\GhostUserRunner.Infrastructure.Tests\GhostUserRunner.Infrastructure.Tests.csproj --filter "FullyQualifiedName~Input|FullyQualifiedName~Browser"
```

Expected: FAIL because mouse-down, click, scroll, text typing, and browser target verification interfaces do not exist.

- [ ] **Step 3: Implement balanced Win32 input primitives**

Add `MouseButtonDown` with flag `0x0002` to `IInputSink` and `Win32InputSink`. Implement `ClickAsync` as move, randomized short hesitation, down, randomized 35–120 ms delay, and up inside `finally`. Implement `ScrollAsync` through the sink. Implement `TypeTextAsync` by mapping letters, digits, spaces, and supported punctuation to virtual keys; hold Shift only when required and release it in `finally`. Reject unsupported characters instead of guessing.

- [ ] **Step 4: Route browser interaction through verified screen coordinates**

Use Playwright only to navigate, find locators, inspect URL/state, and obtain bounding rectangles. Convert locator coordinates to browser-window screen coordinates, reactivate the owned browser, revalidate the locator immediately before input, then call `HumanInputEngine.ClickAsync`, `TypeTextAsync`, and `ScrollAsync`. Remove `locator.ClickAsync`, `PressSequentiallyAsync`, `PressAsync`, and `_page.Mouse.WheelAsync` from user-visible action paths. Send Enter through `TypeAsync([0x0D], token)`.

If focus activation, target resolution, or revalidation fails, return a structured failure without sending input. Keep navigation policy checks before and after page changes.

- [ ] **Step 5: Run Infrastructure and full solution tests**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test GhostUserRunner.sln
```

Expected: PASS, including cancellation tests proving all held input is released.

- [ ] **Step 6: Record the checkpoint**

If Git exists, commit `feat: emit verified browser actions through Win32 input`; otherwise mark Task 3 complete.

### Task 4: Single-Session Application Service and Emergency Monitor

**Files:**
- Create: `src/GhostUserRunner.App/Services/RunnerRuntime.cs`
- Create: `src/GhostUserRunner.App/Services/SessionService.cs`
- Create: `tests/GhostUserRunner.App.Tests/GhostUserRunner.App.Tests.csproj`
- Create: `tests/GhostUserRunner.App.Tests/Services/SessionServiceTests.cs`
- Modify: `GhostUserRunner.sln`

**Interfaces:**
- Consumes: `SessionController`, `BrowserAdapter`, `StopMonitor`, and `SessionLimits`.
- Produces: `ISessionService.StartAsync(TimeSpan? duration, int? diagnosticSeed)`, `PauseAsync()`, `ResumeAsync()`, `StopAsync()`, and `GetStatus()`.
- Produces: `SessionCommandResult(bool Accepted, string Code, string? Message)`.
- Produces: `IRunnerRuntimeFactory.Create(int seed)` returning a new `RunnerRuntime` for one session.
- Produces: `RunnerRuntime` as `IAsyncDisposable`, owning that session's controller, adapters, emergency monitor, and shutdown cleanup.

- [ ] **Step 1: Scaffold the App test project and write failing concurrency tests**

Reference App, Core, Infrastructure, and xUnit packages consistent with existing test projects. Test two simultaneous starts using a `Barrier`; assert one accepted result, one `session.already_active`, and one controller invocation. Test that canceling a synthetic HTTP request token after `StartAsync` does not cancel the session. Test default duration, minimum rejection, pause/stop race, and disposal calling Stop and browser disposal.

- [ ] **Step 2: Run App tests and verify failure**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\GhostUserRunner.App.Tests\GhostUserRunner.App.Tests.csproj
```

Expected: FAIL because the service and runtime do not exist.

- [ ] **Step 3: Implement the runtime composition root**

Move object construction out of `MainWindow` into `RunnerRuntimeFactory`, which retains validated `RunnerOptions` and exposes `Create(int seed)`. Each call constructs a fresh `SeededRandomSource(seed)`, Win32 input, focus coordinator, browser adapter, action policy, planner, controller, and stop monitor. Use the dedicated profile under `%LOCALAPPDATA%\GhostUserRunner\BrowserProfile`; the single-session service prevents concurrent use. `RunnerRuntime.DisposeAsync` stops its controller, cancels its monitor, releases input, and disposes its browser.

- [ ] **Step 4: Implement the non-blocking single-session service**

`StartAsync` validates `duration ?? SessionLimits.DefaultDuration`, chooses `diagnosticSeed ?? RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue)`, asks `IRunnerRuntimeFactory.Create(seed)` for the active runtime, creates an application-owned cancellation source, and starts the controller on a tracked background task. It must not link session lifetime to `HttpContext.RequestAborted`. Protect start/terminal transitions with a semaphore, dispose the active runtime on every terminal path, and observe background exceptions into status. Start `StopMonitor.MonitorAsync` for the same lifetime and route its callback to `StopAsync`.

- [ ] **Step 5: Run App and full solution tests**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test GhostUserRunner.sln
```

Expected: PASS, with the concurrent-start and disconnected-request tests deterministic across repeated runs.

- [ ] **Step 6: Record the checkpoint**

If Git exists, commit `feat: add single-session runner service`; otherwise mark Task 4 complete.

### Task 5: Loopback Web Host and Session API

**Files:**
- Modify: `src/GhostUserRunner.App/GhostUserRunner.App.csproj`
- Replace: `src/GhostUserRunner.App/App.xaml.cs`
- Delete after replacement is working: `src/GhostUserRunner.App/App.xaml`
- Delete after replacement is working: `src/GhostUserRunner.App/MainWindow.xaml`
- Delete after replacement is working: `src/GhostUserRunner.App/MainWindow.xaml.cs`
- Delete after replacement is working: `src/GhostUserRunner.App/Tray/TrayController.cs`
- Create: `src/GhostUserRunner.App/Program.cs`
- Create: `src/GhostUserRunner.App/Web/SessionEndpoints.cs`
- Create: `src/GhostUserRunner.App/Web/SessionDtos.cs`
- Create: `tests/GhostUserRunner.App.Tests/Web/SessionEndpointsTests.cs`

**Interfaces:**
- Consumes: `ISessionService` from Task 4.
- Produces: `POST /api/session/run`, `/pause`, `/resume`, `/stop`, and `GET /api/session/status`.
- Produces: `RunSessionRequest(double? DurationHours, int? DiagnosticSeed)` and JSON status derived from `SessionStatus`.

- [ ] **Step 1: Add failing HTTP contract and binding tests**

Use `WebApplicationFactory<Program>` and a fake `ISessionService`. Cover successful Run, omitted duration selecting eight hours, 7.99/negative/NaN/infinite/huge values returning 400, duplicate Run returning 409, invalid pause/resume transitions returning 409, status returning no-store headers, and a Host-header/origin rejection test. Assert the server configuration contains only loopback URLs.

```csharp
[Theory]
[InlineData(-1)]
[InlineData(0)]
[InlineData(7.99)]
[InlineData(double.NaN)]
[InlineData(double.PositiveInfinity)]
public async Task RunRejectsUnsafeDurations(double hours)
{
    var response = await Client.PostAsJsonAsync("/api/session/run",
        new RunSessionRequest(hours, null));
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}
```

- [ ] **Step 2: Run endpoint tests and verify failure**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\GhostUserRunner.App.Tests\GhostUserRunner.App.Tests.csproj --filter FullyQualifiedName~SessionEndpointsTests
```

Expected: FAIL because the web entry point and endpoints do not exist.

- [ ] **Step 3: Convert App to an ASP.NET Core Windows executable**

Change the project SDK to `Microsoft.NET.Sdk.Web`, retain `TargetFramework` as `net8.0-windows`, remove `UseWPF` and `UseWindowsForms`, and keep `OutputType` as `WinExe`. Add test-visible `public partial class Program { }`. Load and validate `config/appsettings.json` before starting the host. Register one `IRunnerRuntimeFactory` and one `ISessionService`; stop and dispose the service's active runtime during application shutdown.

Configure explicit Kestrel listeners:

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5000);
});
```

Do not read arbitrary URL bindings from environment variables or command-line arguments. Open `http://localhost:5000` with `ProcessStartInfo.UseShellExecute = true` only after `ApplicationStarted` fires.

- [ ] **Step 4: Map guarded API endpoints**

Validate JSON numbers with `double.IsFinite`, convert hours with overflow protection, and enforce `SessionLimits.MinimumDuration`. Map service result codes to 202 Accepted, 400 Bad Request, or 409 Conflict. Add middleware rejecting non-loopback remote addresses, unexpected Host values, and state-changing requests whose `Origin` is present but not `http://localhost:5000` or `http://127.0.0.1:5000`. Apply `Cache-Control: no-store` to API responses.

- [ ] **Step 5: Run endpoint and full solution tests**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test GhostUserRunner.sln
```

Expected: PASS. Verify no WPF types remain with:

```powershell
rg -n "System\.Windows|Windows\.Forms|MainWindow|TrayController" src\GhostUserRunner.App
```

Expected: no matches.

- [ ] **Step 6: Record the checkpoint**

If Git exists, commit `feat: host runner controls on loopback web api`; otherwise mark Task 5 complete.

### Task 6: Dashboard and Live Status

**Files:**
- Create: `src/GhostUserRunner.App/wwwroot/index.html`
- Create: `src/GhostUserRunner.App/wwwroot/styles.css`
- Create: `src/GhostUserRunner.App/wwwroot/app.js`
- Create: `tests/GhostUserRunner.App.Tests/Web/DashboardTests.cs`

**Interfaces:**
- Consumes: Task 5 JSON endpoints.
- Produces: accessible Run, Pause, Resume, and Stop controls with one-second status polling.

- [ ] **Step 1: Add failing dashboard asset tests**

Request `/`, `/styles.css`, and `/app.js`; require success and correct content types. Parse the HTML as text and require `durationHours`, `runButton`, `pauseButton`, `stopButton`, `state`, `elapsed`, `remaining`, `currentActivity`, `lastAction`, `activityLog`, the local-only notice, and `Ctrl+Alt+Q`. Require JavaScript references to all five API routes and uses `textContent`, not `innerHTML`, for log data.

- [ ] **Step 2: Run dashboard tests and verify failure**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\GhostUserRunner.App.Tests\GhostUserRunner.App.Tests.csproj --filter FullyQualifiedName~DashboardTests
```

Expected: FAIL with 404 for the missing assets.

- [ ] **Step 3: Build the single-page dashboard**

Create semantic HTML with an eight-hour numeric input (`min="8"`, `value="8"`, `step="0.5"`), status cards, command buttons, an `aria-live="polite"` error/status region, and a bounded recent-activity list. Keep the design dependency-free and usable at common desktop widths.

In `app.js`, serialize commands so buttons cannot double-submit, poll `/api/session/status` every second, stop overlapping polls with an `AbortController`, format elapsed/remaining values, and derive enabled button states from the returned session state. Render all server text with `textContent`. A page unload only stops polling; it never calls Stop.

- [ ] **Step 4: Run dashboard, App, and full solution tests**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test GhostUserRunner.sln
```

Expected: PASS.

- [ ] **Step 5: Record the checkpoint**

If Git exists, commit `feat: add local simulation dashboard`; otherwise mark Task 6 complete.

### Task 7: Configuration, Packaging, Endurance Verification, and Documentation

**Files:**
- Modify: `config/appsettings.json`
- Modify: `config/appsettings.example.json`
- Modify: `tests/GhostUserRunner.Core.Tests/Planning/EnduranceTests.cs`
- Create: `tests/GhostUserRunner.App.Tests/Web/LoopbackBindingTests.cs`
- Modify: `scripts/publish.ps1`
- Modify: `README.md`
- Modify: `FEATURES.md`

**Interfaces:**
- Consumes: completed web host, planner, API, dashboard, and publish pipeline.
- Produces: a portable executable that opens the local dashboard and an acceptance checklist for supervised real-input testing.

- [ ] **Step 1: Add failing endurance and published-host tests**

Extend the accelerated endurance test to generate enough actions and delays to represent more than eight hours, asserting every delay is `<= SessionLimits.MaximumGeneratedDelay`, all proposals are policy-approved before execution, recent actions vary, and the final simulated elapsed time is over eight hours. Add a loopback configuration test that inspects server addresses and rejects wildcard/LAN endpoints.

- [ ] **Step 2: Run the new tests and verify failure**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test GhostUserRunner.sln --filter "FullyQualifiedName~EnduranceTests|FullyQualifiedName~LoopbackBindingTests"
```

Expected: FAIL until endurance accounting and inspectable host binding are wired.

- [ ] **Step 3: Update configuration and packaging**

Set example and real configuration duration to `08:00:00` and keep pause maximum at or below `40000` ms. Ensure `wwwroot/**` is included in publish output. Keep the existing Playwright Chromium installation and checksum generation. Update the final publish message to show both the executable and `http://localhost:5000`.

- [ ] **Step 4: Update user documentation**

Replace tray/WPF instructions with: publish, start `GhostUserRunner.App.exe`, wait for the dashboard, verify the URL is local, choose at least eight hours, select Run, and use dashboard Stop or `Ctrl+Alt+Q`. Document that closing the dashboard does not stop the runner and that reopening `http://localhost:5000` restores control. Add a supervised smoke checklist for visible pointer movement, clicking, scrolling, typing, pause cleanup, stop cleanup, and emergency stop.

- [ ] **Step 5: Run automated verification**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test GhostUserRunner.sln -c Release
powershell.exe -ExecutionPolicy Bypass -File scripts\publish.ps1
```

Expected: all tests pass and `artifacts\portable\GhostUserRunner\GhostUserRunner.App.exe` plus static dashboard assets are produced.

- [ ] **Step 6: Run the supervised Windows smoke test**

On a disposable desktop session, launch the published executable and confirm:

1. Only `localhost:5000` accepts the dashboard connection.
2. Run with eight hours launches the dedicated Chromium profile.
3. Pointer movement, clicks, scrolling, and typed search text are visible Windows input.
4. No automatic inactivity interval exceeds 40 seconds during a 10-minute observation.
5. Pause releases input and remains paused beyond 40 seconds without resuming automatically.
6. Resume generates a fresh action.
7. Closing and reopening the dashboard does not stop or duplicate the session.
8. Stop and `Ctrl+Alt+Q` each terminate activity and leave no held key or mouse button.

- [ ] **Step 7: Record the final checkpoint**

If Git exists:

```powershell
git add .
git commit -m "docs: ship localhost web runner workflow"
```

Otherwise mark Task 7 complete and report that no commits were created because the source folder has no Git metadata.
