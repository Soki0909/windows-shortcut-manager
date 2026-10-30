# ADR-010: On-demand Shell launch diagnostics

Status: Accepted — implements the owner's #115 investigation without changing runtime architecture.

- Keep Windows .lnk HotKeys and the nonresident Manager. Use a manual read-only diagnostic for the documented WM_GETHOTKEY response-wait mechanism. It is unrelated to enumerating RegisterHotKey registrations.
- Enumerate only current-desktop top-level windows, skip the current process, use 50 ms per query and a roughly 4-second overall budget/1024-window cap. Report incomplete scans, access denial and generic non-response distinctly. Do not collect titles/URLs or send reports externally.
- Candidates are evidence for comparison, not proof. Guide the user to save work, normally close a candidate, then compare real-key response and rerun. Product code never kills a process, toggles OS services or changes driver/security settings.
- The test-only receiver watches only Ctrl, Alt and one selected unused key, only during a manually requested finite session. No hook is installed; no other keystrokes are recorded. It creates an owned Desktop fixture and removes it only when its bytes still match.
- Chrome uses separate serialized queues for visible group operations and history persistence. Explicit group/window selection is temporary; one-way Dispatcher name selection stays backward compatible. Startup automation respects session-scoped explicit group protection.
