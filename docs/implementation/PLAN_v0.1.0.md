# Shortcut Manager v0.1.0 — ExecPlan

This is a living document. Codex updates it during implementation.

## Purpose / Done condition

Deliver a locally usable and release-package-ready `v0.1.0` implementing the requirements and accepted design.

Done requires:
- Windows App builds and tests pass.
- Chrome Extension tests pass.
- four Action Types work.
- managed `.lnk` Apply/rollback works.
- R=研究 and X=7脱 work through generic protocol.
- Manager can close and shortcuts keep working.
- package script creates both release ZIPs + checksums.
- current-machine migration is prepared/applied safely.
- `docs/verification/VERIFICATION.md` contains evidence.

## Progress

- [x] Issue #8 publication (2026-10-28): PR #107 reviewed, CI passed and merged to main at b838d06; v0.1.1 published by Release run 40621230598; downloaded public EXE/ZIPs/checksums passed independent digest, ZIP-layout, version and seven-frame icon checks; owner confirmed taskbar logo. See VERIFICATION for public hashes.
- [x] Issue #8 release preparation (2026-10-28): diagnosed installed old EXE without icon resources; v0.1.1 version alignment and release notes prepared; Core 15/15, Windows 20/20, Chrome 10/10, Release build and package/resource/checksum gates passed. PR review, merge and publication follow the owner's explicit authorization.
- [x] Issue #8 implementation (2026-10-28): owner-supplied logo source/exports preserved; EXE/WPF Window/header/Setup wired; Release build, packages, seven-frame binary comparison, checksums and packaged GUI title/header passed. Shell surfaces and DPI visual matrix remain manual follow-up (see VERIFICATION).
- [x] M11 Startup persisted-shortcut audit and automatic transactional repair (2026-10-27; real COM drift/repair, no-op, ownership and external-conflict tests passed; installed GUI displayed matching audit for all 19 links)
- [x] M12 Per-user installer, lifecycle verification and reproducible packaging (2026-10-27; installation, reinstall, uninstall and final reinstall passed; 404 installed file hashes matched; config and all managed files preserved)
- [x] M0 Repository scaffold + docs imported (2026-09-30)
- [x] M1 Core domain/config/validation/protocol (2026-09-30)
- [x] M2 Windows shortcut engine + scan + transactional Apply (2026-09-30)
- [x] M3 Chrome Extension generic dispatcher v1 (2026-09-30)
- [x] M4 WPF UI implemented, Release-built, and packaged GUI launch/clean-exit smoke passed (2026-09-30; native screenshot capture unavailable in current Computer Use connection)
- [x] M5 Import / backup / restore (2026-09-30)
- [x] M6 Current-machine bootstrap + live usability verification (2026-10-01; 16 conflicts migrated with backup, full 19-key Apply/read-back passed, and post-restart R/X/Z/G/P/A live checks passed)
- [x] M7 CI / packaging / release artifacts (2026-09-30)
- [x] M8 Final documentation / verification / cleanup (2026-10-01; docs, artifacts, automated gates, packaged GUI smoke, and owner live verification complete)
- [x] M9 Live HotKey refresh hardening (2026-10-27; new physical HotKey activated without restarting Windows, success reporting verified, temporary U removed, and no extra foreground Explorer window confirmed)
- [x] M10 Modern WPF UI + physical keyboard layout (2026-10-01; native screenshot and edit-dialog smoke passed with QWERTY rows)

## Discoveries

- Issue #8: application had no ApplicationIcon and used a text glyph in its header. The supplied ICO already contains all seven Windows sizes (16/24/32/48/64/128/256); no conversion or dependency is necessary.
- Startup previously only loaded config; it never compared existing links with resolved launch specifications. The new startup audit repairs missing/changed/obsolete managed links only, and reports external conflicts without modifying them. See ADR-006.
- Installer binaries remain separate from config and managed links. Uninstall preserves both so existing runtime HotKeys keep working and reinstall reuses owner configuration.
- Target machine initially had .NET 6 runtime only. Microsoft .NET SDK 10.0.401 was installed for the required WPF build/test/package workflow.
- Initial inventory implementation enumerated every Desktop file and was too slow; it now searches only `.lnk`/`.url`, skips reparse points, and ignores inaccessible directories. The product scan completes with 243 items after managed Apply.
- WScript.Shell returns modifier text as `ALT+CTRL+R` on this machine. The adapter canonicalizes modifier order to `CTRL+ALT+R` before staged read-back comparison.
- Actual machine inventory found 16 unmanaged file-based HotKey conflicts with the desired enabled layout. The Manager correctly blocked full Apply without changing those source files. After explicit owner authorization, the migration created a 16-file recovery ZIP, cleared only their HotKey fields, and verified zero target HotKeys remained.
- Slack and native ChatGPT App are not installed/detectable. Their desired config entries are present but disabled; LINE and Outlook are represented through existing `.lnk` fallback launch mode.
- The proven unpacked extension ID is still registered at its original path. Formal v0.1.0 files were deployed there with timestamped backups to preserve the ID. Chrome accepted the updated files without a manual reload: direct Protocol v1 Dispatcher smoke tests for 研究 and 7脱 closed cleanly with no residual tab.
- Computer Use exposes Chrome tabs but no native Windows app surface in this session, so WPF screenshot/interaction capture and synthetic HotKey presses are not available through the approved UI tool.
- The final product Apply generated all 19 enabled shortcuts. Product scan read back the exact 19 desired `CTRL+ALT` keys; only external C/K HotKeys remain and neither conflicts with the desired layout.
- Final GUI smoke exposed a startup crash caused by DataGrid columns defaulting to TwoWay binding against read-only `ShortcutRow` projection properties. All read-only columns now explicitly use `Mode=OneWay`; the rebuilt self-contained EXE displayed the `Shortcut Manager` window, responded, accepted normal close, and left zero processes.
- Post-migration live use showed `CTRL+ALT+G` still opening the former 学生ポータル target even though both old `.url` files read back with empty HotKeys and managed `G=GitHub` / `P=学生ポータル` links were correct. This identified stale Explorer shortcut-HotKey state: Apply and migration had changed files without an explicit Shell notification.
- Owner live testing of a newly added `F=一時メモ帳テスト` link proved that synchronous `SHCNE_DELETE/CREATE/UPDATEITEM/UPDATEDIR` notifications still do not make Explorer register a new `.lnk` HotKey during the current Shell session. `CTRL+ALT+F` began working only after Windows restarted. This proves that the persisted link is correct and that notification-only refresh is insufficient; it does not yet prove that restarting Explorer alone is sufficient.
- Microsoft documents `IShellLink::SetHotkey` as setting the shortcut value on a Shell link and `IPersistFile::Save` as persisting the link, but publishes no companion API to rebuild Explorer's active Start-menu HotKey table. `SHChangeNotify` publishes item/directory change events, not a HotKey-registration refresh event. `RegisterHotKey` registrations belong to the calling window or thread, so using it would require a resident owner and violate the non-resident architecture. References: [IShellLinkW::SetHotkey](https://learn.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishelllinkw-sethotkey), [Shell Links](https://learn.microsoft.com/windows/win32/shell/links), [SHChangeNotify](https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shchangenotify), [RegisterHotKey](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey).
- The Windows SDK headers installed on the development machine contain link setters and unrelated window/process HotKey APIs, but no documented Explorer HotKey-table refresh operation. Explorer is the component that tracks Start-menu/Desktop shortcut HotKeys, as described by Microsoft in [The Old New Thing](https://devblogs.microsoft.com/oldnewthing/20130528-00/?p=4233).
- The first instrumented Explorer-only live run on 2026-10-27 changed the Shell PID from `12764` to `27160`, proving the taskbar/Desktop Shell actually restarted without restarting Windows. The app nevertheless reported failure because the old process disappeared between Shell discovery and a subsequent `Process` metadata access. Treat that already-exited race as idempotent, but require observation of a different current-session Shell PID before reporting success.
- After that Explorer-only restart, the owner physically pressed `CTRL+ALT+U` and the newly configured Notepad target opened. This passes the product-level requirement that an Apply can activate a new `.lnk` HotKey without restarting Windows while the Manager remains non-resident afterward.
- A first race fix covered termination, but the instrumented rerun still changed Explorer PID `27160 → 26816` while reporting failure. The remaining race was the stale Shell-window PID disappearing inside `GetShellProcess` or `WaitForExitAsync`; both discovery and wait are now idempotent for an already-exited process, while the final success condition still requires a different current-session PID.
- Successful reporting was then verified with Explorer PID `26816 → 27328`. The owner noted that immediately calling `explorer.exe` could also open a foreground file-manager window. Windows normally auto-restarts the Shell, so the reloader now waits up to five seconds for that replacement before using `Process.Start` only as a recovery fallback. This avoids racing the OS auto-restart and opening an unnecessary Explorer window.

## Decision Log

- Owner authorized push, PR review, main merge and release on 2026-10-28. Publish the logo update as v0.1.1 rather than replacing v0.1.0 artifacts or moving an existing tag. Preserve the historical v0.1.0 verification evidence.
- Issue #8: retain owner-supplied artwork unchanged, embed the ICO in EXE and WPF resources, and use it in Setup. Existing Start-menu/desktop launchers and uninstall metadata resolve the EXE icon. Record owner-provided provenance without asserting an independently verified copyright license. Add a read-only native-resource check to CI/Release; do not reinstall or replace an already published release during this change.
- Use an optional noninteractive command mode (`--scan`, `--validate-config`, `--apply-config`, `--restore-backup`, each with `--result`) in the same WPF executable. This supports reproducible local verification while using the identical composition root and Apply/Restore services as the GUI.
- Use source `.lnk` fallback mode for user-scoped installed apps because COM expansion under the managed execution identity can resolve per-user environment variables differently; launching the existing `.lnk` preserves its original app semantics.
- Apply the nonconflicting desired keys I/L/V first to prove real Start Menu ownership and rollback surfaces without touching unmanaged shortcuts. After owner approval, migrate the exact 16 conflicts with original-byte backup and then use the same product Apply engine for the full layout.
- Preserve the public executable name as `ShortcutManager.exe` via `AssemblyName`; keep the internal project boundary named `ShortcutManager.App`.
- Send `SHChangeNotify(SHCNE_UPDATEITEM/SHCNE_UPDATEDIR, SHCNF_PATHW | SHCNF_FLUSH)` for every generated/modified shortcut and its directory after migration, successful Apply, and Apply rollback so Explorer is explicitly told to invalidate shortcut state. This follows the Shell API contract and avoids relying solely on filesystem watchers.
- Treat replacement as an explicit Shell lifecycle: notify deletion after moving each old managed shortcut away, then notify creation after moving each staged shortcut into place. This makes HotKey unregister/register observable without changing the non-resident `.lnk` architecture.
- Shell notifications remain useful for filesystem/UI cache coherence but are not a HotKey registration guarantee. After successful Apply, offer an explicit, warned restart of only the current session's Shell Explorer process; identify it through `GetShellWindow`, never target another session, and relaunch Explorer before reporting success. Keep a separate `Shellを再読込` recovery action.
- Do not emulate the shortcut Properties dialog or call undocumented Shell messages. Those approaches depend on private implementation details and are too brittle for a safety-focused release. If the explicit Explorer reload is not confirmed by the live gate, state that sign-out/Windows restart is required instead of claiming immediate activation.
- Keep WPF and the dependency-free UI stack. Modernization uses shared native WPF resources, while the A-Z overview follows the physical `QWERTYUIOP / ASDFGHJKL / ZXCVBNM` key order.

## Milestones

### M0 — Scaffold
Deliver:
- exact repository structure
- solution/projects
- AGENTS.md
- docs imported
- scripts placeholders that execute
Validation:
- `dotnet restore`
- solution builds empty/minimal projects
- extension manifest parses

### M1 — Core
Deliver:
- config models
- JSON polymorphism
- validators
- hotkey model
- dispatcher URI builder
- filename sanitizer
- apply-plan types
Validation:
- Core unit tests green

### M2 — Windows engine
Deliver:
- WScript Shell adapter
- `.lnk` reader/writer
- `.url` parser
- inventory scanner
- Chrome locator
- launch resolver
- managed-folder marker
- transactional apply/rollback
Validation:
- Windows tests green
- temp `.lnk` read/write HotKey roundtrip

### M3 — Extension
Start from proven PoC v1.1.2.
Replace hardcoded actions with protocol v1 + group query.
Deliver:
- ES module pure libs
- generic Dispatcher
- popup generic group list
- startup collapse setting
Validation:
- `node --test`
- load unpacked
- direct Research/7脱 dispatcher smoke

### M4 — WPF
Deliver exact UI from design.
Validation:
- launch app
- create/edit/delete candidate
- hotkey capture
- dirty close
- Test target
- no crash on invalid candidate

### M5 — Import / backup / restore
Validation:
- real existing shortcut scan
- import preview
- auto backup
- forced rollback test
- restore test

### M6 — Current machine
Deliver:
- `local/` gitignored
- local bootstrap config generated from target layout and actual machine
- application targets resolved from installed shortcuts
- Extension ID configured locally
- new 作業開始 BAT
- backup then Apply
Validation:
- R, X
- representative Website
- representative Application
- Z BAT
- Manager closed + HotKeys still work

### M7 — CI/package
Deliver:
- ci.yml
- release.yml
- package.ps1
- SHA256
Validation:
- local package
- inspect ZIP structure
- workflow syntax review

### M8 — Finish
Deliver:
- README quick start
- CHANGELOG
- SECURITY
- VERIFICATION
- no leftover PoC-only hardcoding
- no personal absolute path committed
- Initial commit `b76312d` (`feat: complete Shortcut Manager v0.1.0`) was created and pushed to `origin/main` on 2026-10-01 after owner authorization. GitHub Release publication was not performed.

### M9 — Live HotKey refresh
Deliver:
- explicit Shell delete/create/update notifications during Apply
- matching rollback notifications
- regression tests for replacement, removal, and rollback notification order
Validation:
- focused Windows tests
- full Release test gate
- live Apply on the owner machine without restarting Windows

### M10 — Modern UI / keyboard layout
Deliver:
- shared WPF color, typography, input, card, and button styles
- clearer header, status, action hierarchy, and shortcut table
- physical three-row keyboard layout
Validation:
- key-order unit test
- Release build
- packaged/native GUI screenshot and interaction smoke

## Verification Evidence

- Owner confirmed the new v0.1.1 taskbar logo on 2026-10-28. Installed v0.1.0 had no native icon resources; its Start-menu launcher points to that older EXE. No product fix beyond the new icon-bearing build or icon-cache reset was necessary for the taskbar check.
- Issue #8 (2026-10-28): `dotnet build ShortcutManager.sln -c Release --no-restore --nologo` passed with zero warnings/errors; `scripts/package.ps1`, `scripts/package-installer.ps1 -SkipPublish`, and `scripts/verify-branding.ps1` passed. All seven EXE/Setup icon frames are byte-identical to the supplied ICO and all three artifact SHA-256 values match. Computer Use inspection of the newly packaged local executable showed the logo in the title bar and header and a healthy startup audit.
- M11/M12 (2026-10-27): Core 15/15, Windows 20/20, Chrome 10/10. Real COM audit covers missing, modified and obsolete links; reconciliation covers backup-backed repair, a no-write healthy start, and no-write external-conflict rejection. Inno Setup 6.7.3 generated the Setup EXE. Actual per-user install/reinstall/uninstall/reinstall all exited 0; config and 19 managed links plus ownership marker remained byte-identical. Installed GUI launched from its Start-menu launcher and displayed the matching-audit status. All 404 installed payload files and all three artifact SHA-256 values matched.
- Final M9/M10 audit (2026-10-27): Core 15/15, Windows 17/17, Chrome 10/10; package regeneration and independent SHA-256/ZIP checks passed. Final packaged GUI displayed a responsive window and closed normally. Owner confirmed new physical U activation without restarting Windows, U removal, and no extra foreground Explorer window after automatic Shell recovery. All changes are committed and pushed to `codex/live-refresh-modern-ui`.
- Modern UI verification: Release build completed with 0 warnings/errors; native Windows capture showed the three centered `QWERTYUIOP / ASDFGHJKL / ZXCVBNM` rows, modern card/action hierarchy, clipped long targets, and the Q tile opened the correct ChatGPT Web editor without changing data.
- Keyboard layout regression test verifies all 26 unique letters in physical-row order; Core 15/15, Windows 11/11, and Chrome 10/10 passed after the UI work.
- Packaged `ShortcutManager.exe` loaded the current host configuration and displayed the modern UI. A temporary `F=一時メモ帳テスト` entry was applied successfully, but physical `CTRL+ALT+F` did not work until Windows restarted. The result disproved the notification-only approach and now serves as the regression scenario for the consent-based Shell reload flow.
- The post-change package gate completed and produced both ZIPs plus checksums. Windows PowerShell 5 compatibility required reading the UTF-8 extension manifest explicitly rather than through its ANSI-default `Get-Content` behavior.
- `dotnet test ShortcutManager.sln -c Release --no-restore`: Core 15/15 and Windows 11/11 passed (follow-up gate).
- `node --test tests/ChromeExtension.Tests/protocol.test.js`: 10/10 passed.
- Real COM integration: `.lnk` target/arguments/HotKey roundtrip passed.
- Product scan: 240 items before managed Apply; 243 after, including 3 managed shortcuts with `CTRL+ALT+I/L/V`.
- Safe real Apply: generated 3 shortcuts, config SHA-256 `c492d4293e657f273949ba5ca11102060ba4a35e2f78b52883d6aabdc7516324`.
- Real Restore: I/L/V → I-only probe → backup restore to I/L/V succeeded; generated link count returned to 3.
- Forced commit failure rollback is covered by Windows integration test and restores prior link/config bytes.
- `scripts/package.ps1 -Version 0.1.0` created both ZIPs, checksums, self-contained EXE, and root-manifest smoke passed (final gate).
- Packaged GUI smoke after the binding fix: window title `Shortcut Manager` appeared within one second, `Responding=True`, `CloseMainWindow=True`, clean exit completed within five seconds, and zero `ShortcutManager` processes remained.
- `scripts/migrate-unmanaged-hotkeys.ps1 ... -PlanOnly` validated all 16 exact conflict targets without modifying them. The execution path creates a ZIP with original bytes, paths, original HotKeys and SHA-256, clears only HotKey fields, verifies read-back, and restores changed files on failure.
- Owner-authorized migration completed under elevation: 16/16 original shortcuts and the manifest are in `local/migration-backups/unmanaged-hotkeys-20260930-212648408.zip` (SHA-256 `6686ae0e96594c5c2cc784d3b8353e1537c1467337ac2653844e1d95c411dded`); product scan found all 16 paths and zero remaining HotKeys.
- Migration rollback reporting was hardened after the non-elevated preflight exposed a permission failure: rollback now attempts every changed file and reports both the original failure and every restore failure instead of masking the original error. PowerShell AST parse and post-migration `-PlanOnly` (16 paths, no writes) passed.
- Full product Apply completed: 19 generated shortcuts, config SHA-256 `283db1121cfd85e2fb4fe9538a5bb51f922f5bc085d5e63d3dbb71ab4a0dc936`, automatic pre-Apply backup `20260930-212800778_2c224551a3ab481ebe7bc7cc67edc26d.zip`. Post-Apply scan found 19 managed links and the exact 19 expected keys with no missing/extra key.
- Shell notification fix: Windows tests and PowerShell AST/PlanOnly passed; the full 19-link config was reapplied successfully with per-item and directory notifications and backup `20261001-013805033_c68bd4408f504f63b9dccb5fb39772ca.zip`. After restarting Windows, physical `G=GitHub` and `P=学生ポータル` checks both passed, confirming the prior behavior was stale Explorer session state.
- Direct Chrome launch of canonical Protocol v1 URLs for `研究` and `7脱`: browser inventory stayed at 19 tabs, target groups remained present (3 and 7 tabs), and Dispatcher tab count returned to zero for both. This distinguishes the updated generic parser from the old PoC fixed-action parser.
- Owner final live verification after restart: with Shortcut Manager closed, all requested representative HotKeys worked without issue: `R=研究`, `X=7脱`, `Z=作業開始`, `G=GitHub`, `P=学生ポータル`, and `A=LINE`. R/X focused the intended Chrome groups with no Dispatcher tab left behind.
- Final 2026-10-01 audit reran every automated gate successfully, verified both artifact hashes against `SHA256SUMS.txt`, inspected the Windows ZIP (404 entries, expected EXE present) and extension ZIP (16 entries, root manifest, no `package.json`), scanned 259 shortcuts with the packaged executable, matched the exact 19-key managed set, and found zero resident Manager processes.

## Blockers

- No remaining blockers for M9/M10. Physical U activation and removal passed; corrected reload success was logged; the managed configuration returned to 19 enabled shortcuts. The final automatic-recovery run changed Explorer PID `32368 → 45204` in about 0.9 seconds, and the owner confirmed no extra file-manager window appeared in front.
- Native ChatGPT App and Slack are absent; their final-layout entries remain disabled until installed or an owner-approved launch target is supplied.
- Chrome Web Store/Microsoft Store publication and code signing require owner credentials/accounts and are not v0.1.0 package blockers.
