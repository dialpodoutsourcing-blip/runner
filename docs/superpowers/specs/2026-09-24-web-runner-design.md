# Ghost User Runner Web Control Design

**Status:** Approved design  
**Date:** 2026-09-24  
**Target:** Windows desktop with a localhost web dashboard

## 1. Purpose

Ghost User Runner will replace its WPF and tray-facing session controls with a web dashboard served by a local Windows runner. The user starts the runner, opens its localhost URL, and selects **Run** to begin a randomized simulation that controls the real Windows pointer and keyboard inside a dedicated browser environment.

Success means that a user can start and manage a session entirely from the local web dashboard, each normal session lasts at least eight hours, generated inactivity never exceeds 40 seconds, behavior varies without becoming unsafe or meaningless, and all existing shutdown and policy safeguards remain effective.

## 2. Scope

### Included

- A local web dashboard with Run, Pause, Resume, and Stop controls.
- Live session state, elapsed and remaining time, current activity, most recent action, and a concise activity log.
- Real Windows pointer movement, safe clicks, scrolling, and synthetic keystrokes.
- A dedicated Chromium profile and browser window controlled by the runner.
- Randomized multi-step browser activity constrained by approved domains and recognized controls.
- A default and minimum normal session duration of eight hours.
- Randomized delays with a hard maximum of 40 seconds between generated activities.
- Existing emergency shutdown, input cleanup, action policy, and structured logging behavior.

### Excluded

- Remote or local-network access to the dashboard.
- Controlling an existing personal browser profile or arbitrary foreground application.
- Purchases, downloads, uploads, authentication, messages, posts, comments, or form submission.
- Physical keystroke capture, stealth, CAPTCHA bypass, or shutdown resistance.
- Clicking, typing, or navigating when the target cannot be recognized and policy-approved.

## 3. Architecture

The application remains a local Windows program. It embeds an ASP.NET Core server bound only to `127.0.0.1` and serves both the dashboard and its control API. A normal webpage cannot produce trusted system-wide input by itself, so the existing Windows runner remains responsible for mouse and keyboard synthesis.

The application is divided into the following units:

- **Web host:** Starts the loopback-only HTTP server, serves the dashboard, and optionally opens the dashboard URL in the user's default browser.
- **Dashboard:** Displays current state and sends explicit session commands to the local API.
- **Session API:** Validates commands, rejects conflicting requests, and exposes a read-only status stream or polling endpoint.
- **Session controller:** Owns the single active session and coordinates planning, execution, pause, recovery, completion, and shutdown.
- **Activity planner:** Generates varied semantic action chains, applies recency penalties, and ensures every planned wait is at most 40 seconds.
- **Safety supervisor and adapters:** Validate browser destinations, controls, focus, and action categories before allowing input.
- **Windows input engine:** Produces visible pointer paths, clicks, scrolling, and synthetic typing, with cancellation between atomic events.

Only one simulation may be active. The web layer never calls the input engine or application adapters directly; all commands pass through the session controller and existing policy checks.

## 4. Dashboard and API

The dashboard is a single local page containing:

- A duration control that defaults to eight hours and rejects shorter values.
- A **Run** control when stopped or completed.
- A **Pause** control while running and **Resume** while paused.
- A **Stop** control whenever a session is active.
- State, elapsed time, remaining time, current activity, last completed action, and recent generated-action events.
- A visible statement that the controller is available only on the current PC.
- The emergency shutdown shortcut.

The API exposes commands equivalent to:

- `POST /api/session/run`
- `POST /api/session/pause`
- `POST /api/session/resume`
- `POST /api/session/stop`
- `GET /api/session/status`

Command handlers are idempotent where practical and return a conflict response for invalid transitions, including attempting to start a second session. The initial implementation may poll status at a short interval; a streaming transport is unnecessary unless polling proves inadequate.

## 5. Session Behavior

Selecting **Run** performs the following sequence:

1. Validate configuration and the requested duration.
2. Reject a duration shorter than eight hours.
3. Create a session and cancellation scope.
4. Launch a dedicated Chromium instance and clean automation profile.
5. Focus and verify the owned browser window.
6. Generate and execute policy-approved activity chains until the duration expires or the user stops the session.
7. Release held input, close runner-owned transient resources safely, and record the terminal state.

Production sessions receive a fresh random seed. Diagnostic execution may accept an explicitly configured seed so a failure can be reproduced. The planner varies activity type, target, pointer path, typing cadence, scrolling, and dwell time while penalizing recent choices and preventing exact short-term repetition.

Every planned pause must be greater than or equal to the configured safe minimum and less than or equal to 40 seconds. This limit applies to hesitation, dwell time, recovery backoff, and ordinary between-action delays. It does not cause activity while the user has explicitly paused the session; an explicit pause lasts until Resume or Stop.

Pausing cancels the current interruptible operation, releases any held mouse buttons or keys, and preserves the session's remaining duration. Resuming asks the planner for a fresh next action instead of replaying a partially completed input sequence.

## 6. Input and Browser Safety

The runner launches and controls only its dedicated Chromium profile. It does not attach to an existing personal browser or reuse saved credentials, cookies, payment details, or extensions.

Before each visible input operation, the runner verifies the expected process, window, browser origin, and semantic target. Mouse movements may traverse the desktop, but clicks occur only on recognized, policy-approved targets. Keystrokes are sent only after the expected safe field has been located and focus has been confirmed. Loss of verification cancels the input rather than guessing.

Existing default-deny restrictions remain in force. The runner blocks downloads, uploads, purchases, authentication, communication, settings changes, permission prompts, CAPTCHAs, unknown dialogs, and form submission. It does not record physical keyboard input.

`Ctrl+Alt+Q` remains an independent emergency stop. Every pause, stop, failure, and process-exit path releases held keys and mouse buttons.

## 7. State and Data Flow

The externally visible session states are stopped, starting, running, pausing, paused, stopping, completed, and failed.

1. The dashboard sends a command to the loopback API.
2. The API validates the requested state transition and delegates it to the session controller.
3. The session controller requests the next action chain from the planner.
4. The action policy validates each proposed semantic action.
5. The browser and desktop adapters locate and verify the target.
6. The Windows input engine performs the visible interaction.
7. The adapter observes the result and reports a structured outcome.
8. The controller updates session status, history, timing, and the activity log.
9. The dashboard reads the updated status.

The dashboard receives only runner-generated status and log data. It does not receive arbitrary screen contents, captured physical input, credentials, or unrelated personal data.

## 8. Error Handling

- Invalid startup configuration prevents the session from entering the running state and produces an actionable dashboard error.
- A transient page or target failure is retried once after full revalidation.
- A failed retry abandons the current chain and selects another eligible safe activity.
- A stalled or crashed dedicated browser is restarted into a known approved page when recovery remains safe.
- Focus loss stops input before focus recovery is attempted.
- Unknown dialogs, inconsistent state, policy violations, or repeated failures terminate the session safely.
- Recovery backoff remains subject to the 40-second maximum while a session is running.
- An API or dashboard disconnection does not remove local emergency-stop protection. The runner continues the already-started session unless explicitly stopped or a safety condition requires termination.

## 9. Security Boundary

The web server binds exclusively to the IPv4 and IPv6 loopback interfaces and must not listen on wildcard, LAN, or public addresses. The dashboard does not support remote access. State-changing requests validate that they originated from the local dashboard and use same-origin protections. Responses containing status or logs are marked to prevent caching where appropriate.

The web interface is a controller for a local trusted process, not a hosted web application. Deployment does not open a firewall port or expose controls to other computers.

## 10. Testing and Acceptance

Automated tests cover:

- Valid and invalid API state transitions.
- Prevention of concurrent sessions.
- Rejection of session durations shorter than eight hours.
- Default eight-hour duration.
- Randomized production seeds and reproducible diagnostic seeds.
- Planner variation, history penalties, and repeat cooldowns.
- The invariant that every planned running-state pause is at most 40 seconds.
- Pause, stop, failure, and shutdown input cleanup.
- Dedicated-browser creation and allowlisted navigation.
- Focus loss, stalled pages, browser crashes, unknown dialogs, and emergency stop.
- Loopback-only server binding and same-origin command checks.
- An accelerated endurance test representing more than eight hours of planning and state transitions.

Acceptance also requires a supervised live-desktop smoke test confirming that the pointer, clicks, scrolling, and typing are visible; targets remain restricted; dashboard controls respond; the emergency stop works; and no key or mouse button remains held after cancellation.

## 11. Delivery Boundary

The first web release replaces the user-facing WPF session controls with the localhost dashboard while reusing the existing core, policy, adapter, and input layers. It supports one local session, one dedicated browser instance, browser-focused randomized activity, an eight-hour minimum duration, and the 40-second maximum generated pause.

Remote control, multiple simultaneous sessions, arbitrary desktop-application automation, user accounts, cloud hosting, and advanced dashboard analytics are deferred.
