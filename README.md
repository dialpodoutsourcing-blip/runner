# Ghost User Runner

Ghost User Runner is a Windows desktop automation project that produces long-running, human-like but strictly constrained PC activity. It can browse approved websites, search for and watch YouTube videos, operate browser windows, browse approved folders in File Explorer, and open safe files in read-only applications.

The runner generates fresh activity procedurally. It is not a recorded macro and does not capture a person's keystrokes.

## Project status

Local MVP implemented. It is designed for supervised local testing before any long unattended session.

## Run locally

1. Keep disposable files only in `C:\vibec\runner\SafeFiles`, or update `config/appsettings.json` to another dedicated folder.
2. Run `powershell.exe -ExecutionPolicy Bypass -File scripts/publish.ps1` to build the portable application and install its private Chromium copy.
3. Open `artifacts\portable\GhostUserRunner\GhostUserRunner.App.exe`. It starts a local-only controller and opens `http://localhost:5000`.
4. Choose a duration of at least eight hours and select **Run simulation**. The runner launches its dedicated Chromium profile and performs randomized visible Windows mouse and keyboard input.
5. Use the dashboard to pause, resume, or stop. Closing the dashboard does not stop the session; reopen `http://localhost:5000` to restore control.
6. To stop without the dashboard, hold `Ctrl+Alt+Q` for two seconds or enter the upper-left corner three times within five seconds.

Generated activity never schedules an automatic pause longer than 40 seconds. A pause explicitly requested from the dashboard remains paused until Resume or Stop.

Before every browser, File Explorer, or Notepad action, the runner restores and foregrounds the expected window. Other approved windows remain open behind it; the runner does not type into whichever window happens to be on top.

Test in short supervised sessions first. The optional installer requires Inno Setup 6 and can be created by adding `-Installer` to the build command.

## Documents

- [Design specification](docs/superpowers/specs/2026-09-22-ghost-user-runner-design.md)
- [Feature roadmap](FEATURES.md)

## Supervised smoke check

Before an unattended session, confirm that the dedicated browser opens, mouse movement/clicking/scrolling and typed searches are visible, Pause stops input, Resume continues with a fresh action, and both Stop and `Ctrl+Alt+Q` release all input.

## Non-negotiable safety boundaries

- Approved websites, folders, file types, and applications only.
- No purchases, downloads, uploads, messages, posts, or form submissions.
- No deleting, renaming, moving, copying, or editing files.
- No physical keystroke logging, stealth, CAPTCHA bypass, or authentication handling.
- Immediate shutdown through `Ctrl+Alt+Q` or the mouse-corner failsafe.
