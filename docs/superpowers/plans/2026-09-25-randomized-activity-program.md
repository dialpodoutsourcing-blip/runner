# Randomized Activity Program Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate a seeded, non-repetitive activity program at session startup and execute varied multi-step browser/file activity sets.

**Architecture:** Replace per-loop weighted selection with a prebuilt queue of `ProposedAction` activity sets carrying seeded step parameters. The existing controller consumes the queue, while adapters execute each set atomically and the lifecycle executor rotates the single owned window using seeded per-set close decisions.

**Tech Stack:** .NET 8, C#, xUnit, Microsoft Playwright, Win32 input APIs

**Spec:** `docs/superpowers/specs/2026-09-25-randomized-activity-program-design.md`

## Global Constraints

- No idle activity is generated.
- Exact activity signatures remain out of the generated schedule for at least ten estimated minutes.
- Adjacent activity templates differ.
- Only allowlisted HTTPS origins, folders, extensions, and viewers are used.
- At most one runner-owned application window is visible.
- Pause and Stop cancel waits and input immediately.

## Review Focus

- Too few unique topics: generation must terminate and vary step parameters without weakening safety.
- Missing safe files: file-reading sets must be excluded without failing browser generation.
- Failed page selectors: stop the set with a structured outcome and preserve exclusive ownership.
- Long schedules: an eight-hour program must remain bounded in memory and deterministic by seed.
- Cancellation during a multi-step set: input must be released and the owned window closed during session cleanup.

---

### Task 1: Prebuilt seeded activity program

**Files:**
- Create: `src/GhostUserRunner.Core/Planning/ActivityProgram.cs`
- Modify: `src/GhostUserRunner.Core/Planning/ActivityPlanner.cs`
- Modify: `src/GhostUserRunner.Core/Actions/ProposedAction.cs`
- Test: `tests/GhostUserRunner.Core.Tests/Planning/ActivityPlannerTests.cs`
- Test: `tests/GhostUserRunner.Core.Tests/Planning/EnduranceTests.cs`

**Interfaces:**
- Produces: `ActivityProgram` with a bounded queue of `ProposedAction`; action parameters `scrollCount`, `scrollDirection`, `readingSeconds`, and `closeAfter`.

- [ ] **Step 1: Write failing tests** proving same-seed equality, different-seed variation, no adjacent equal kinds, no signature reuse inside thirty estimated twenty-second slots, and eight-hour queue coverage.
- [ ] **Step 2: Run** `dotnet test tests/GhostUserRunner.Core.Tests/GhostUserRunner.Core.Tests.csproj -c Release --filter FullyQualifiedName~Planning` and confirm the new assertions fail against dynamic selection.
- [ ] **Step 3: Implement** a constructor-built program whose weighted template selection excludes the preceding kind and recent signatures, selects only eligible file templates, and embeds seeded step parameters in each action.
- [ ] **Step 4: Run the focused tests** and require zero failures.
- [ ] **Step 5: Commit** with `feat: generate seeded activity programs`.

### Task 2: Varied multi-step browser activity sets

**Files:**
- Modify: `src/GhostUserRunner.Infrastructure/Browser/BrowserAdapter.cs`
- Modify: `src/GhostUserRunner.Core/Planning/ActivityDwellTime.cs`
- Test: `tests/GhostUserRunner.Infrastructure.Tests/Browser/BrowserPolicyInterceptorTests.cs`
- Test: `tests/GhostUserRunner.Infrastructure.Tests/Input/HumanInputEngineTests.cs`

**Interfaces:**
- Consumes: the Task 1 action parameters.
- Produces: policy-gated multi-scroll search, YouTube, and Wikipedia activity outcomes.

- [ ] **Step 1: Write failing tests** for bounded parameter parsing, alternating scroll direction, visible query keystrokes, and cancellation propagation.
- [ ] **Step 2: Run focused infrastructure tests** and confirm failures identify missing multi-step behavior.
- [ ] **Step 3: Implement** repeated varied scrolling, bounded reading/watch intervals, Google/YouTube typed search, and direct allowlisted Wikipedia navigation without adding tabs or bypassing route guards.
- [ ] **Step 4: Run focused tests** and require zero failures.
- [ ] **Step 5: Commit** with `feat: execute varied browser activity sets`.

### Task 3: Seeded window rotation

**Files:**
- Modify: `src/GhostUserRunner.Infrastructure/Execution/CompositeActionExecutor.cs`
- Test: `tests/GhostUserRunner.Infrastructure.Tests/Execution/CompositeActionExecutorTests.cs`

**Interfaces:**
- Consumes: `closeAfter` from each prebuilt browser activity.
- Produces: browser reuse until the scheduled close point while maintaining one owned window.

- [ ] **Step 1: Write failing tests** proving browser reuse across a variable number of sets, scheduled close execution, application-switch closure, and no fixed three-action cycle.
- [ ] **Step 2: Run the executor tests** and confirm the fixed counter causes failure.
- [ ] **Step 3: Remove** `BrowserActionsPerWindow`; close only when `closeAfter` is true or when switching application families.
- [ ] **Step 4: Run executor tests** and require zero failures.
- [ ] **Step 5: Commit** with `feat: randomize browser session lifetime`.

### Task 4: Full verification and release readiness

**Files:**
- Modify: `config/appsettings.json` only if template weights need adjustment.
- Modify: `config/appsettings.example.json` identically.

- [ ] **Step 1: Run** `dotnet test GhostUserRunner.sln -c Release -v minimal` and require zero failures.
- [ ] **Step 2: Run** `powershell.exe -ExecutionPolicy Bypass -File tests/verify-release-package.ps1` and require success.
- [ ] **Step 3: Run** `git diff --check` and inspect the complete diff for allowlist or lifecycle regressions.
- [ ] **Step 4: Commit** any final test/config corrections as `test: verify randomized activity programs`.
