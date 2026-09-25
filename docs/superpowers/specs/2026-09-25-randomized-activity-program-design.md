# Randomized Activity Program Design

## Goal

Make an eight-hour simulation behave like varied, intentional computer use instead of repeating a short browser loop. Each run must generate a reproducible randomized activity program before execution, keep recent work from repeating for ten minutes, and never own more than one visible application window at a time.

## Program generation

At session start, a seeded program generator builds an ordered queue of activity sets covering the requested duration. The queue is generated from weighted activity templates, but it may not place the same template consecutively. It also tracks each template-and-subject signature and excludes signatures used within the preceding ten simulated minutes.

The generated program is deterministic for a diagnostic seed. A production run receives a fresh random seed. The dashboard status and event log continue to expose the current activity and outcomes without exposing unsafe controls.

The initial template catalog is:

1. Google research session
2. YouTube discovery session
3. Wikipedia reading session
4. Safe-folder browsing session
5. Safe text-file reading session

If a file template has no eligible safe file, the generator substitutes another eligible template. It never emits an idle activity. If cooldown rules temporarily exclude every exact signature, it generates a new subject for an eligible template; it does not weaken the domain, path, extension, or application allowlists.

## Activity sets and steps

An activity set is a small stateful script rather than one coarse action. Its steps are also seeded and randomized.

Google research opens or reuses the dedicated browser, enters a fresh query through visible human-paced keystrokes, waits for results, performs multiple varied up/down scrolls, and may navigate to an allowlisted result or return to results.

YouTube discovery opens YouTube in the same dedicated browser window, enters a fresh query through visible keystrokes, scrolls the result list, selects an available video, watches for a bounded randomized interval, and may return to results.

Wikipedia reading navigates directly to an allowlisted Wikipedia subject, reads with varied pauses, and performs multiple up/down scrolls.

Safe-folder browsing opens a dedicated Explorer process for an approved root and keeps it visible for the activity set. Safe-file reading opens an approved file with the configured viewer and performs only supported read-only interactions. The process is closed at the end of the set or on failure.

Steps within a set must remain policy-gated. Downloads, uploads, authentication, form submission, messaging, editing, and destructive file operations remain prohibited.

## Timing and variety

The existing cancellable ten-second gap remains between completed activity sets, not between every internal step. Internal typing, pointer movement, reading, watching, and scrolling use bounded randomized timing.

An exact activity signature consists of the template, subject or target, and significant step choices. The same signature cannot recur within ten minutes. The same template cannot occur twice consecutively. Subjects and scroll direction or distance vary between sets.

Browser lifetime is randomized rather than tied to a fixed action count. A browser session contains several browser activity sets and closes only after a seeded minimum has been reached and a randomized close decision succeeds, with a hard maximum to guarantee eventual rotation. Closing, reopening, and switching to Explorer or the viewer are represented as lifecycle events, not planner-visible idle work.

## Window and resource lifecycle

The runner owns at most one visible application window. Browser activity sets reuse one dedicated Chromium window. Before starting Explorer or the text viewer, the runner closes Chromium. Explorer and viewer processes are launched separately, tracked by owned process handles, and closed before another set begins.

Stop, cancellation, failure, and normal session completion close the currently owned browser, Explorer, or viewer. The runner never closes an unrelated user window.

## Components

`ActivityProgramGenerator` creates the complete seeded queue at session start from activity templates, subjects, duration, cooldown rules, and eligibility.

`ActivitySet` describes a template, unique signature, estimated duration, and ordered steps. Steps use the existing closed `ActionKind` vocabulary, extended only where a safe granular browser interaction needs a distinct kind.

`SessionController` consumes the prebuilt queue, policy-checks every step, records step and set outcomes, and applies the ten-second inter-set delay.

`CompositeActionExecutor` retains exclusive window ownership and routes each step to the browser or file executor. Browser lifecycle thresholds become seeded program decisions instead of the current fixed three-action counter.

`BrowserAdapter` executes granular navigation, search, back, scroll, click, and bounded reading/watch steps while rechecking focus and allowlist state before input.

## Failure handling

A changed or missing page element returns a structured failed outcome. The current activity set stops, its owned window is closed when ownership is uncertain, and the controller proceeds only if policy and session state permit recovery. Any allowlist violation fails closed. Pause and Stop interrupt internal waits and the inter-set delay immediately.

## Testing

Unit tests will prove that identical seeds generate identical programs, different seeds produce varied sequences, adjacent templates differ, exact signatures stay outside a ten-minute window, and no idle set is generated.

Program tests will cover unavailable files, duration coverage, finite fallback behavior, and an eight-hour schedule. Executor tests will verify ordered multi-step browser sessions, visible keystroke search entry, varied scrolling, randomized browser rotation within bounds, exclusive window ownership, and cleanup on success, failure, cancellation, and shutdown.

The full solution and release-package checks must pass before release.
