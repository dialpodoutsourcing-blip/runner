# Ghost User Runner Design Specification

**Status:** Approved design  
**Date:** 2026-09-22  
**Target:** Windows desktop

## 1. Purpose

Ghost User Runner creates convincing, long-running PC activity for a harmless household prank. It operates visibly through normal desktop interfaces and generates new behavior during each session rather than replaying a recording.

Success means the runner can operate safely for at least 8-10 hours, produce varied human-like browser and File Explorer activity, remain inside explicit allowlists, recover from common failures, and stop immediately on demand.

The runner generates synthetic input only. It does not capture a person's keystrokes or attempt to conceal its existence.

## 2. Scope

### Included

- Approved-site browsing and web searches.
- YouTube searches and video playback.
- Browser tab and window management.
- File Explorer navigation within approved folder roots.
- Opening approved file types in approved read-only viewers.
- Human-like mouse movement, scrolling, typing, pauses, and idle periods.
- Procedural activity planning with repetition avoidance.
- Visible status, local action logs, recovery, and emergency shutdown.

### Excluded

- Purchases, downloads, uploads, messages, posts, comments, or form submissions.
- File deletion, creation, editing, copying, moving, or renaming.
- Authentication, CAPTCHA bypass, or access to saved personal credentials.
- Physical keystroke capture, surveillance, stealth, or shutdown resistance.
- Unrestricted website, folder, file-type, or application access.

## 3. Architecture

The application uses a hybrid controller. Reliable automation APIs determine and validate targets, while visible synthetic pointer and keyboard input makes supported interactions appear natural.

### 3.1 Session controller

The session controller owns the application lifecycle and is the only component allowed to start or stop an automation session. It:

- Starts sessions with a configured duration or no time limit.
- Coordinates planning, execution, pausing, recovery, and shutdown.
- Exposes running, paused, recovering, stopping, stopped, and failed states.
- Responds to the tray controls, global stop hotkey, and corner failsafe.
- Ensures held input is released and owned child processes are cleaned up.

### 3.2 Activity planner

The planner constructs multi-step activity chains from registered action types. It selects chains and individual actions using weighted randomness constrained by current state and policy.

Its history records recent activity categories, search topics, query text, video identifiers, domains, folders, files, applications, and timing patterns. Recent items receive a configurable selection penalty. Hard cooldowns prevent exact repeats within configured windows.

The planner may generate activities such as:

- Search the web, inspect results, visit an approved page, scroll, and return.
- Search YouTube, choose a result, watch part of it, adjust playback, and switch tabs.
- Open File Explorer, browse approved folders, open a safe file, close it, and return.
- Resize or minimize a window, idle, restore it, and continue.

Every planned action remains a proposal until the safety supervisor approves it.

### 3.3 Human-input engine

The input engine generates visible synthetic interaction. It supports:

- Curved pointer paths with variable velocity, acceleration, overshoot, and correction.
- Click timing and double-click timing within realistic configured ranges.
- Variable scroll distance, direction, cadence, and pauses.
- Synthetic typing with variable key intervals, word pauses, and optional generated corrections.
- Cancellation between atomic input events so shutdown remains responsive.

The input engine never subscribes to general physical keyboard events. A narrowly scoped global hotkey listener recognizes only the configured control commands.

### 3.4 Application adapters

Adapters expose semantic operations and observable state without giving the planner direct access to automation libraries.

- **Browser adapter:** Uses Playwright with a dedicated browser profile. It exposes approved navigation, search, YouTube playback, scrolling, and tab/window operations.
- **File Explorer adapter:** Uses Windows UI Automation to navigate approved folder roots and select approved files.
- **Viewer adapters:** Use Windows UI Automation to recognize approved read-only viewers, verify the opened file, and close the viewer.
- **Desktop adapter:** Handles approved window switching, minimizing, maximizing, restoring, resizing, and focus verification.

Each adapter reports structured outcomes: success, transient failure, policy rejection, unknown state, or fatal failure.

### 3.5 Safety supervisor

The safety supervisor is mandatory for every action. It validates:

- The active process, window, and recognized UI state.
- Browser origin and destination domains.
- File system paths after canonical resolution.
- File extension and assigned viewer.
- Proposed action category and semantic target.
- Whether the target resembles a blocked control or workflow.

The supervisor uses default-deny behavior. Unknown applications, dialogs, controls, websites, folders, and file types are not acted upon.

## 4. Randomness and natural variation

Randomness is hierarchical rather than uniform:

1. Select an activity category from configured weights.
2. Apply eligibility rules for the current application and state.
3. Penalize recent categories and targets.
4. Select or generate a safe target.
5. Produce a variable sequence of semantic actions.
6. Produce fresh motion, typing, scrolling, and timing parameters for each action.

The planner varies behavior across short, medium, and long timescales. It may alternate active periods with short pauses and occasional long idle periods. It must not execute meaningless random clicks or keys; variation always occurs inside a recognized safe operation.

Production sessions use cryptographically seeded pseudorandom generation. Diagnostic sessions accept an explicit seed so failures can be reproduced.

## 5. Configuration

Configuration is external to the executable and validated before a session begins. It includes:

- Approved web domains and search providers.
- Approved search-topic categories and blocked terms.
- Approved folder roots, file extensions, and viewer applications.
- Approved desktop applications.
- Activity weights, cooldowns, and recent-history limits.
- Mouse, typing, scrolling, pause, and idle timing ranges.
- Session duration or unlimited mode.
- Hotkeys, failsafe behavior, retry limits, and failure thresholds.
- Logging level and retention.

Invalid or overly broad entries fail closed with a clear configuration error. Folder roots and executable paths are resolved to canonical absolute paths before acceptance.

## 6. Safety policy

### 6.1 Web policy

The browser uses a dedicated profile with no personal history, passwords, cookies, payment information, extensions, or existing authenticated sessions. Navigation is permitted only to configured HTTPS domains.

The runner blocks downloads, uploads, purchases, authentication, messaging, posting, commenting, account settings, permission grants, and form submission. It abandons CAPTCHAs, advertisements, pop-ups, unexpected redirects, and unknown dialogs.

### 6.2 File policy

File Explorer access is limited to configured roots. Both requested and observed paths must remain beneath a root after canonical resolution. Network shares, removable drives, shortcuts to outside locations, archives, executables, scripts, installers, and unapproved extensions are rejected.

The runner may navigate, select, open, view, and close. It may not delete, create, save, edit, rename, move, copy, share, print, compress, extract, upload, or change permissions.

### 6.3 Input and shutdown policy

Holding `Ctrl+Alt+Q` for two seconds initiates immediate shutdown. Moving the pointer rapidly into the upper-left corner three times provides an independent failsafe. A visible tray menu provides normal pause and stop commands.

Shutdown cancels the current plan, prevents new actions, releases held keys and buttons, closes runner-owned transient windows where safe, flushes logs, and exits. The runner does not conceal itself or resist termination through Windows tools.

## 7. Data flow

1. The session controller requests the next chain from the activity planner.
2. The planner examines current state, configuration, and recent history.
3. The planner emits one proposed semantic action at a time.
4. The safety supervisor validates the proposal and observed target.
5. The appropriate adapter locates and verifies the target.
6. The input engine performs the visible interaction where appropriate.
7. The adapter observes and classifies the result.
8. The controller records the generated action and outcome.
9. The planner updates history and chooses the next action or chain.

No component may bypass safety validation to invoke an adapter or the input engine.

## 8. Logging and privacy

Logs describe only runner-generated activity and internal outcomes. They may contain timestamps, random seed identifiers, action categories, generated search text, approved URLs, approved file paths, results, and recovery events.

The application does not record physical keystrokes, clipboard contents, screen video, unrelated window content, personal browser data, or credentials. Log retention is configurable and defaults to a bounded local history.

## 9. Error handling and recovery

- A transient page or UI failure is retried once after revalidation.
- A failed retry abandons the chain and selects another safe activity.
- A browser crash restarts the dedicated profile and restores a safe approved page.
- An invalid File Explorer location returns to a configured root.
- An unknown dialog or lost focus halts input before recovery is attempted.
- Repeated recoverable failures cause a safe idle period.
- Policy violations, inconsistent observed state, or failure thresholds stop the session.
- A watchdog ensures cancellation and input cleanup even if an adapter stalls.

Recovery never relaxes policy constraints or clicks through an unknown state.

## 10. User interface

The initial user interface is a visible Windows tray application with:

- Current session state and elapsed time.
- Current activity category and most recent safe action.
- Start, pause, resume, and stop commands.
- Links to configuration and local logs.
- Clear display of the active stop hotkey and corner failsafe.

Configuration may initially use a documented file, with a graphical editor deferred until the core runner is proven reliable.

## 11. Testing and acceptance

### 11.1 Automated testing

- Unit tests cover configuration validation, canonical path enforcement, domain checks, blocked-action policy, weighted selection, cooldowns, history penalties, and state transitions.
- Adapter tests cover recognized UI states and structured outcomes.
- Input tests cover cancellation and cleanup without measuring subjective realism.
- Simulation executes thousands of proposed actions without controlling the live desktop and asserts that every executed proposal was policy-approved.
- Failure injection covers browser crashes, missing folders, network loss, unexpected dialogs, focus loss, adapter timeouts, and stop requests during input.

### 11.2 Endurance testing

An accelerated test simulates at least 12 hours of planning and state transitions. A supervised live-PC soak test then runs with the final allowlists and dedicated browser profile before any unattended session.

### 11.3 Acceptance criteria

The first release is acceptable when it:

- Completes a supervised 12-hour session without an unsafe action.
- Stops promptly through both shutdown mechanisms during every activity type.
- Never leaves a synthetic key or mouse button held after stopping or failing.
- Rejects every tested blocked web, file, and communication operation.
- Recovers from supported transient failures without leaving the allowlisted environment.
- Produces varied activity without exact chain repetition inside configured cooldowns.
- Records enough generated-action detail to reproduce seeded diagnostic sessions.

## 12. Initial delivery boundary

The first implementation will prioritize the controller, safety supervisor, simulation mode, stop mechanisms, dedicated-browser operation, and basic File Explorer navigation. More elaborate humanization and additional read-only viewers will be added only after policy enforcement and shutdown behavior pass automated and supervised tests.
