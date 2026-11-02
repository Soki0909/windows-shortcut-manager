# v0.3.1 — Apply and Restore reliability

## Progress and priority

- [x] Select P1 #62, #64, #65, #66 as a bounded safety release. Preserve the owner's 22 keys; no launch-contract changes or new dependencies.
- [x] Share semantic launch comparison across staging, committed-file readback and startup audit. Verify before the terminal journal marker.
- [x] Recover the Shell after timeout/cancellation using an independent bounded budget; do not terminate a second process or act on another session.
- [x] Preserve Restore summary and Shell pending state independently of reload/status messages; offer consent-based restart after Restore.
- [x] Explain backup exclusions and diagnose missing external references before Restore.
- [x] Local regression, Debug GUI and build/package checks (CI follows in the PR).
- [ ] Merge to main, publish verified public assets, update installed product and extension with backup and hash verification.

## Discoveries and decisions

- The staged check omitted working directory, icon, description and window style. There was no semantic readback after moving links. The journal terminal marker now follows full committed readback.
- Timeout-specific catch skipped the old general recovery attempt. All failures after a termination attempt now share recovery, independent of caller cancellation. Recovery confirms the Shell only; it does not claim that the requested restart/HotKey refresh succeeded.
- A separate Restore notice survives ordinary status changes and reload. Declining restart leaves the pending flag; only a verified successful restart clears it within this Manager session. Windows still has no public HotKey registration-table query.
- New transaction tests initially ran in parallel with existing tests and correctly hit the global Apply mutex. Group both test classes in a nonparallel collection, preserving the production mutex behavior.
- No requirements or accepted architecture change. Product data is never a fault-injection fixture.

## Validation evidence

- `dotnet test ShortcutManager.sln -c Release`: Core 27/27, Windows 98/98 passed. Includes all seven attribute mismatch cases in stage and committed paths, COM normalization, post-verification process kill/recovery, absent external BAT/source link, Restore notice/pending state and Shell failure/cancellation/recovery fake-host cases.
- Extension Node regression 22/22 passed. Extension runtime code is unchanged; only manifest version alignment, so no new personal-Chrome automation or live group mutation is needed.
- `scripts/run-debug.ps1`: Debug build passed with zero warnings/errors. Native GUI: scope explanation, Save ZIP, restore preview, empty Debug restore, consent offer, decline and persistent notice passed. Only Debug empty config was restored; no production HotKeys or Shell restart during this check. A duplicate status/notice was then simplified, with focused ViewModel checks repeated.
- Local Release package, Inno Setup build, all seven EXE/Setup icon frames, three SHA-256 checksums, manifest/ZIP/version contract passed. EXE/Setup remain NotSigned as documented. CI builds/packages final committed source and exercises installer lifecycle on a disposable runner.
