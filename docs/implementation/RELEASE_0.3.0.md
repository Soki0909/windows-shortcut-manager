# v0.3.0 — responsiveness and Chrome reliability

## Progress and priority

- [x] #115: reproduce real HotKey latency, identify Shell-response candidates, owner-approved comparison, and add a bounded on-demand diagnostic window/CLI.
- [x] #47: all recognized Worker requests terminate with success/failure; Popup handles transport/storage failure, retry and unknown delivery without automatic replay.
- [x] #48: Popup selects the temporary group/window instance, independently of Dispatcher name selection.
- [x] #84: collapse-all separates success, failure, disappearance and skipped automatic targets.
- [x] #85: serialize history read/modify/write and preserve the 100-title cap.
- [x] #86: serialize startup/group-created work with explicit operations and retain explicit protection through Worker restart in session storage.
- Publication and installed-product update are tracked through the PR, tagged Release workflow and private local update evidence. Keep the owner's existing 22 keys and four empty keys.

## Discoveries and decisions

- An owned, disposable executable reproduced a roughly 3-second delay **before target code ran** when launched by a physical Windows shortcut key. Direct execution of the same link did not have that delay. This is distinct from Chrome startup/Worker handling.
- Read-only WM_GETHOTKEY probes found unresponsive windows owned by two other applications. After explicit owner approval to close those applications, probes had no candidates and real-key latency fell to approximately 0.14 seconds. This supports a Shell response-wait explanation; the two applications were changed together, so individual causality was not established.
- Product diagnostics never terminate an app or change OS/driver/security settings. Query each window with a short timeout, bound total work, separate access-denied/unknown failure, collect no window titles/URLs, and identify incomplete scans. No global hook or resident Manager is introduced.
- Keep dispatch toggles in order. Startup automation must not overwrite the final explicit state (an intentional toggle-collapse remains collapsed). Session protection is browser-session-only and survives Worker restart; IDs are not added to Windows config.
- Return visible focus success without waiting for a redundant history persistence write. Later history reads still wait for queued writes. Tests hold that write unresolved to verify the response does not wait.

## Validation evidence

- Core 27/27, Windows 70/70, extension 20/20 passed. Windows tests use an isolated child window that deliberately delays WM_GETHOTKEY, and verify bounds/cancellation. No fault is injected into owner applications.
- Real Chrome run `chrome-live-1793343256430`: all checks passed, including each duplicate Popup row, message/storage failures and control recovery, Worker restart, cold Chrome, generated links, tab preservation, unhandled-exception checks and cleanup. The executable source was unchanged after this run except probe reporting and version alignment.
- Actual latency measurement: [LATENCY_2026-10-30.md](../verification/LATENCY_2026-10-30.md). Raw samples/process IDs and owner paths stay gitignored under local/.
- A test initially used checkbox.check(), which correctly failed because the product immediately reverted a failed save. The test now clicks and asserts rollback/retry rather than requiring the failed value to remain checked.
