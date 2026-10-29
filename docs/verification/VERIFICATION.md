# Shortcut Manager Verification

Updated: 2026-10-29 (Asia/Tokyo)

## Issue #110 and nine safety improvements (unreleased)

Branch: `codex/ten-issues-110-safety`. Details: [implementation tracker](../implementation/ISSUES_110_AND_SAFETY.md), [ADR-008](../architecture/ADR-008-one-shot-chrome-group-toggle.md).

- Executed Core Debug 24/24, Windows Debug 43/43, Node 12/12. Isolated fault injection covers recovery failures, unmanaged-file/root protection, corrupt config, backup integrity/limits/atomic replacement and Restore conflicts. Worker tests cover toggle transitions and compatibility.
- Debug built without warnings and created its separate window via run-debug.ps1. Visual inspection and real HotKey gates remain pending: Windows computer-use stopped because browser URL could not be determined confidently.
- Dedicated Chrome for Testing 148 profile and extension copy prepared under gitignored local/. No product install/config/HotKey or personal Chrome extension was changed. No Debug config was created.
- Do not count #110 complete or accept ADR-008 until the live matrix passes. Ten-issue goal remains active; no Issue has been closed merely for adding code.
- Final Release gate: `dotnet test ShortcutManager.sln -c Release --no-restore --nologo` passed Core 24/24 and Windows 43/43; `node --test tests/ChromeExtension.Tests/protocol.test.js` passed 12/12. `scripts/package.ps1 -Configuration Release -Version 0.1.1` passed. Both generated SHA-256 values independently matched SHA256SUMS, the extension included lib/group-operation.js and excluded the live harness. These are local unreleased artifacts, not replacements for the published v0.1.1.
- Follow-up: PR #111 CI run 40830979235 passed both dotnet (1m57s) and extension (12s). Raising the Debug window allowed a matching screenshot to confirm its DEBUG title, D badge, empty key layout, Apply button and separate managed directory. The isolated CfT window still triggered computer-use URL detection failure; no HotKey checks were counted as passed.

## Development/product isolation (2026-10-28, unreleased)

- `scripts/run-debug.ps1` builds and launches the exact `ShortcutManager.Debug.exe`; observed process title `Shortcut Manager [DEBUG]`, responsive process. Release solution build succeeds with the original `ShortcutManager.exe` name. Both builds report zero warnings/errors.
- Debug uses `ShortcutManager.Debug` LocalAppData and `Shortcut Manager Debug` managed links. A copied config's managed-folder override cannot redirect Debug writes. Unit/real-COM regression coverage verifies independent paths, unchanged Release path overrides, Apply/Restore preserving product sentinels, and conflict rejection preserving prior debug config/link bytes.
- Windows tests: 24/24 passed in Release and Debug. Debug Core 15/15 and Chrome 10/10 passed. `package.ps1 -Configuration Debug` rejected its parameter before touching publish output.
- Real Debug launch preserved all 22 SHA-256 values covering the installed EXE, product config and managed files. It did not load the installed config automatically or update the installation. The owner-requested workflow is persisted in AGENTS.md and ADR-007.
- GUI screenshot/interaction verification was interrupted by the owner's physical Escape. No further Computer Use input was issued. D-badge rendering and simultaneous taskbar-group appearance remain visual checks; process identity/title and storage isolation are verified automatically. The previously owner-confirmed v0.1.1 product logo taskbar check remains passed.

## Issue #8 — product logo (2026-10-28)

- Owner confirmed the newly packaged v0.1.1 displays the new logo in the taskbar on 2026-10-28. This passes the requested taskbar check; the installed old EXE remains unchanged until the owner runs the update installer.
- Follow-up for v0.1.1: the installed v0.1.0 EXE has no RT_GROUP_ICON resources (0 matching logo frames), and the Start-menu launcher points to that installed EXE with the default icon reference. This explains the old taskbar appearance before installing an update; the packaged build is a separate executable. No cache reset or Explorer restart was needed to establish the cause.
- v0.1.1 candidate: Core 15/15, Windows 20/20, Chrome 10/10 passed; Release solution build passed with zero warnings/errors; package and installer generation passed. All seven new EXE/Setup frames and all three artifact checksums passed `scripts/verify-branding.ps1 -Version 0.1.1`. Application, backup metadata, About, extension and packaging versions are aligned to 0.1.1; protocol/schema remain 1.
- Owner supplied the SVG, transparent PNG exports and multi-resolution ICO and requested integration. Originals are retained under `assets/branding/` and application `Assets/`; provenance is documented without asserting a separate third-party license or independent rights clearance.
- Release solution build passed with zero warnings/errors. Initial sandbox build was interrupted; the normal-host retry passed.
- `scripts/package.ps1 -Version 0.1.0` and `scripts/package-installer.ps1 -Version 0.1.0 -SkipPublish` passed with Inno Setup 6.7.3.
- `scripts/verify-branding.ps1 -Version 0.1.0` reads native RT_GROUP_ICON/RT_ICON resources as data: EXE and Setup each contain byte-identical copies of all supplied 16/24/32/48/64/128/256px ICO frames. All three generated artifact SHA-256 values match `SHA256SUMS.txt`. CI and Release now run this verification.
- Computer Use inspection of the executable under `artifacts/publish/win-x64/` confirmed the logo in the title bar and header, an intact layout and a matching startup audit. The installed older executable was distinguished from the new build by its full returned process path.
- Existing installer Start-menu/desktop launchers and `UninstallDisplayIcon` use `ShortcutManager.exe`, which now embeds the logo. The initial local implementation did not install or publish an update. No Explorer icon-cache reset was performed.
- Remaining manual checks: Alt+Tab and the visual matrix across multiple DPI settings, and installed Start-menu/uninstall-list rendering after an upgrade. The owner limited the follow-up visual check to the taskbar, which passed. Do not treat native-resource validation as a passed full visual matrix.

## Environment

- Windows x64 target machine
- .NET SDK `10.0.403` (2026-10-27 follow-up)
- Node.js `v22.15.0`
- Chrome: `C:\Program Files\Google\Chrome\Application\chrome.exe`
- Product version / config schema / Dispatcher protocol: `0.1.0 / 1 / 1`

## Automated verification

| Area | Command | Result |
|---|---|---|
| Core + Windows | `dotnet test ShortcutManager.sln -c Release --no-restore` | Core 15/15, Windows 20/20 passed |
| Extension | `node --test tests/ChromeExtension.Tests/protocol.test.js` | 10/10 passed |
| Version/policy | `scripts/verify-version.ps1 -ExpectedVersion 0.1.0` | Passed; MV3, no commands/host_permissions/content_scripts/externally_connectable |
| Release build | `dotnet build ShortcutManager.sln -c Release --no-restore` | Passed with 0 warnings / 0 errors |
| Packaging | `scripts/package.ps1 -Version 0.1.0` | Passed; self-contained EXE and root manifest verified |
| Packaged GUI smoke | Start self-contained EXE, wait for window, request normal close | `Shortcut Manager` window appeared within 1 second; responding; clean exit; 0 remaining processes |

Tests cover config roundtrip/polymorphism, Unicode, schema/URL/Extension ID validation, disabled invalid target behavior, GUID/HotKey conflicts, filename sanitation, Dispatcher percent encoding, real WScript `.lnk` roundtrip, `.url` numeric HotKey decoding (`1602 → CTRL+ALT+B`), scan root dedupe, managed ownership rejection, staging/read-back/backup/commit, idempotence, external conflict blocking, forced rollback, Restore through the Apply transaction, and Chrome protocol/group/tab selection.

## 2026-10-27 startup audit and installation

- GUI startup now compares persisted config with the actual owned links: existence, resolved target, arguments, working directory, canonical HotKey, icon, description and window style. Extra obsolete links are detected. COM's equivalent icon forms (`exe` / `exe,0`) are normalized to avoid repeated false repairs.
- Differences are repaired through the existing backup/stage/rollback transaction, followed by an offer to restart Explorer. Healthy startup does not write or create a backup. First launch without config and invalid-config load do not auto-apply an empty configuration. Unexpected files/subfolders, missing ownership, invalid configuration or external conflicts stop automatic repair.
- Real COM tests modified target arguments/HotKey, added an obsolete link and removed an expected link, then verified repaired equality and backup existence. The exact startup reconciliation service also passed healthy no-write and external-conflict no-write cases. Windows tests: 20/20; Core: 15/15; Chrome: 10/10.
- `scripts/package-installer.ps1 -Version 0.1.0` generated self-contained portable and extension ZIPs plus `ShortcutManager-Setup-win-x64-v0.1.0.exe`. Build tool: Inno Setup 6.7.3, downloaded from its official repository, SHA-256/signature checked. No application dependency was added. CI/release workflows now build and publish the Setup artifact alongside ZIPs.
- Actual per-user installation, same-version update/reinstall, uninstall and final reinstall all exited 0. Windows uninstall registration and Start-menu launcher were verified. All 404 installed payload files matched the published files by SHA-256. Config plus all 20 managed-folder files (19 links and marker) remained byte-identical through the lifecycle. User data/backups and managed runtime links are deliberately preserved on uninstall.
- Installed app startup logged `repaired=False, differences=0` for the real saved configuration. Launching from the installed Start-menu shortcut opened the expected executable; native accessibility inspection confirmed the visible status `起動時監査: 保存済み設定とショートカットは一致しています。`. Normal close passed.
- Installer is unsigned; Authenticode signing and Store publication remain separate owner-credential work. No additional manual gate remains for these features. Audit verifies persisted link data, not Explorer's private active HotKey table or other processes' runtime key registrations.

## 2026-10-01 live-refresh and UI follow-up

- Apply reports changed shortcut lifecycle events to Explorer synchronously: deletion for links moved out of service, then creation/update for committed links, plus the managed-directory update. Rollback emits the matching reverse lifecycle. Windows regression tests verify replacement, removal, and rollback notification order. Live testing proved these notifications are necessary for Shell cache coherence but insufficient to rebuild Explorer's `.lnk` HotKey registration table.
- The packaged native GUI loaded the current host configuration and displayed three physical keyboard rows in `QWERTYUIOP / ASDFGHJKL / ZXCVBNM` order. The refreshed card, input, table, status, and action hierarchy were inspected in the running Windows application; opening the Q editor preserved the existing data.
- A temporary `F=一時メモ帳テスト` application entry targeting `C:\Windows\System32\notepad.exe` was added and applied successfully. Physical `CTRL+ALT+F` did not work in the existing Explorer session and worked after Windows restarted. This proves the `.lnk` was persisted correctly and the notification-only approach is insufficient, but does not by itself prove that restarting Explorer alone is sufficient.
- The GUI now offers a warned, consent-based reload of only the current session's Shell Explorer after Apply and as a separate recovery action. The implementation resolves the Shell PID through `GetShellWindow`, refuses another session, restarts Explorer, waits for the replacement Shell, and reports the old/new PIDs. It deliberately says only that Shell was reloaded and asks the owner to verify the HotKey; it does not claim activation before that physical check.
- Public API research found no documented command equivalent to the shortcut Properties dialog's HotKey Apply behavior. `IShellLink::SetHotkey` and `IPersistFile::Save` persist the `.lnk`; `SHChangeNotify` provides file/directory lifecycle notifications; neither documents rebuilding Explorer's active HotKey table. `RegisterHotKey` would require the registering window/thread to remain alive, which conflicts with the product's non-resident requirement. See [IShellLinkW::SetHotkey](https://learn.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishelllinkw-sethotkey), [Shell Links](https://learn.microsoft.com/windows/win32/shell/links), [SHChangeNotify](https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shchangenotify), and [RegisterHotKey](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey).
- On 2026-10-27 the owner added `U=一時メモ帳テスト` and Apply succeeded with 20 generated links plus automatic backup `20261027-112732538_06e74b58021041a6a10d43187fd92769.zip`. The consent-based Explorer action changed the Shell PID from `12764` to `27160` without restarting Windows. Physical `CTRL+ALT+U` then opened Notepad, passing the no-Windows-restart live gate. The app incorrectly reported failure because the old PID disappeared between Shell discovery and process metadata access; the implementation now treats that already-exited race as idempotent and only reports success after observing a different current-session Shell PID.
- The first reporting fix covered termination, but the next integrated run still reported failure while Explorer successfully changed `27160 → 26816`. The remaining exception came from resolving or waiting on a stale Shell-window PID after process exit. `GetShellProcess`, `Terminate`, and `WaitForExitAsync` now all treat an already-exited process as an expected race; session mismatch and failure to observe a different replacement PID remain hard failures.
- The fully race-tolerant rerun succeeded and logged Explorer PID `26816 → 27328`. After removing the temporary U through the product transaction (backup `20261027-113726005_13d0087eb0454aa78eca3ab09c77fe80.zip`), another successful reload logged `27328 → 32368`; the owner physically confirmed `CTRL+ALT+U` no longer did anything. Product scan found exactly 19 managed links, zero U entries, and the persisted config returned to 19 enabled shortcuts with its original SHA-256 `283db1121cfd85e2fb4fe9538a5bb51f922f5bc085d5e63d3dbb71ab4a0dc936`.
- The owner also observed an unnecessary foreground Explorer window during Shell recovery. The reloader now gives Windows' automatic Shell restart a five-second grace period and calls `explorer.exe` only if no replacement Shell appears. Unit coverage distinguishes the auto-restart path (no explicit launch) from the recovery fallback.
- Current automated follow-up results: Core 15/15, Windows 17/17, Chrome 10/10, and Release build 0 warnings/errors. The final automatic-recovery run logged `success=True`, Explorer PID `32368 → 45204`, and completed in about 0.9 seconds. The owner confirmed that no additional file-manager window appeared in front. The physical activation, physical removal, success-reporting, and no-foreground-window live gates all passed.

## Current-machine evidence

- Product command-mode inventory read 240 unmanaged `.lnk/.url` items before Apply, 243 total after safe Apply, and 259 total after full Apply (19 managed shortcuts).
- Chrome has currently open groups named `研究` and `7脱` (observed via connected Chrome tab inventory).
- Existing proven PoC route has `CTRL+ALT+R` at `Windows Shortcut Manager PoC\Research - Chrome Tab Group.lnk`.
- Formal Tab Bridge v0.1.0 was copied over the same unpacked-extension path with timestamped directory backups, preserving extension ID `plmgefhiiengedjjpiheeobbcclokojf`. Manifest/file read-back passed.
- Canonical Protocol v1 Dispatcher URLs for `研究` and `7脱` were launched directly. Before and after each call Chrome inventory stayed at 19 tabs, target groups remained present (3 `研究` tabs and 7 `7脱` tabs), and zero `chrome-extension://` Dispatcher tabs remained. The old PoC parser would reject `action=focus-group`, so this also proves Chrome loaded the formal generic implementation.
- External `作業開始` BAT was backed up and updated to exactly six lines opening ChatGPT Web, Notion, and Google Drive, with no Gmail/Slack/LINE/TimeTree. Read-back verification passed.
- Gitignored `local/bootstrap-config.json` represents the requested A-Z layout. Native ChatGPT App and Slack entries are disabled because those applications were not detected.

## Real Apply / Backup / Restore

The actual product `ManagedShortcutApplier` was used against the default user Start Menu managed folder; no hand-authored final `.lnk` was used.

1. Preflight confirmed the managed folder and config did not exist.
2. Nonconflicting desired keys `I=Gemini`, `L=DeepL`, `V=Canva` were applied.
3. Apply result: success, 3 generated links, config hash `c492d4293e657f273949ba5ca11102060ba4a35e2f78b52883d6aabdc7516324`.
4. Product scan read back each managed `.lnk` as `CTRL+ALT+I/L/V`, target `explorer.exe`, with the expected quoted URL and no parse errors.
5. Same config was re-applied successfully (idempotence).
6. A restore probe changed managed state to one `I` link.
7. `--restore-backup` validated the backup, automatically backed up current state, and restored all 3 links through the same Apply transaction.
8. Forced commit failure integration test verifies prior config and link bytes are restored; unmanaged nonempty folder protection test verifies refusal without the marker.

After explicit owner authorization, the final conflict migration and full Apply were executed:

9. `scripts/migrate-unmanaged-hotkeys.ps1` created `local/migration-backups/unmanaged-hotkeys-20260930-212648408.zip`, containing the manifest plus all 16 originals. ZIP SHA-256: `6686ae0e96594c5c2cc784d3b8353e1537c1467337ac2653844e1d95c411dded`.
10. Product scan found all 16 exact source paths and zero remaining HotKeys.
11. Full `--apply-config local/bootstrap-config.json` succeeded with 19 generated links and automatic backup `20260930-212800778_2c224551a3ab481ebe7bc7cc67edc26d.zip`.
12. Persisted config SHA-256 is `283db1121cfd85e2fb4fe9538a5bb51f922f5bc085d5e63d3dbb71ab4a0dc936`, matching the Apply result.
13. Post-Apply product scan found 19 managed links and all 19 expected `CTRL+ALT` key letters, with no missing or extra managed key. External HotKeys C and K remain, but do not overlap the configured set.
14. The migration rollback error path was hardened to continue restoring all changed files and preserve both original and restore-error diagnostics. PowerShell AST parsing reported zero errors; a post-migration `-PlanOnly` revalidated all 16 exact paths without writes.
15. Live use initially reported stale `CTRL+ALT+G → 学生ポータル` behavior despite correct file read-back (`G=GitHub`, `P=学生ポータル`, old files empty). Apply and migration now send synchronous per-item and directory Shell change notifications after changes and rollback. Tests passed and the 19-link config was reapplied successfully with backup `20261001-013805033_c68bd4408f504f63b9dccb5fb39772ca.zip`.
16. Explorer process `15756` started at `2026-09-27 17:01:45`, before the `2026-09-30 21:26:49` migration. This supports the diagnosis that the current Explorer session may retain pre-migration HotKey registrations; a sign-out/restart is the remaining clean invalidation test if live G/P remains stale.
17. Owner live verification after Windows restart passed: `CTRL+ALT+G → GitHub` and `CTRL+ALT+P → 学生ポータル`. This confirms the managed links are correct and the earlier G behavior was retained pre-migration Explorer state.
18. Owner final live verification passed all requested representative paths with Shortcut Manager closed: `R=研究`, `X=7脱`, `Z=作業開始`, `G=GitHub`, `P=学生ポータル`, and `A=LINE`. The R/X group routes focused Chrome correctly and left no Dispatcher tab behind.

## WPF GUI smoke

Final audit initially found a real startup crash: the read-only DataGrid projection properties were being bound with the WPF default TwoWay mode. The columns now explicitly use `Mode=OneWay`. After rebuilding and repackaging, the self-contained `ShortcutManager.exe` displayed a responsive `Shortcut Manager` window within one second, accepted a normal window-close request, exited cleanly within five seconds, and left zero processes. The packaged command mode also completed a real scan of 259 shortcuts. The follow-up native inspection verified the modern UI, physical three-row key order, existing-config load, editor interaction, and successful Apply status.

## Full-layout conflict migration

The full desired Apply was initially blocked because these 16 enabled keys were owned by unmanaged shortcuts: A, B, D, E, G, M, N, O, P, Q, R, T, W, X, Y, Z. Important paths included the old PoC R shortcut, old Gemini X shortcut, and old `ctrl_alt_z.bat.lnk` Z shortcut.

Full evidence with exact paths is stored only in gitignored `local/inventory.json` because it contains owner-specific absolute paths.

`scripts/migrate-unmanaged-hotkeys.ps1` was added as the recoverable migration mechanism. Its `-PlanOnly` run first validated all 16 exact paths. After explicit authorization, elevated execution backed up all originals and cleared only their HotKey fields; it did not delete any unmanaged shortcut. Read-back found zero remaining HotKeys on the 16 targets.

## Manual/live verification

- Passed after Windows restart: `G=GitHub`, `P=学生ポータル`, `R=研究`, `X=7脱`, `Z=作業開始`, and `A=LINE`.
- Representative HotKeys continued to work with Shortcut Manager closed, confirming the Manager is non-resident and Windows `.lnk` HotKeys are the runtime trigger.
- R/X focused their intended Chrome groups, brought Chrome forward, and left no Dispatcher tab behind.
- Native Windows UI inspection passes for the modern layout and edit flow. The integrated Apply → consent prompt → Explorer PID change → physical HotKey check passed without restarting Windows. Corrected success reporting, temporary U removal, and automatic Shell recovery without an extra foreground window also passed. No manual gates remain for M9/M10.

## Publication status

### GitHub Release v0.1.1 — published 2026-10-28

- [PR #107](https://github.com/Soki0909/windows-shortcut-manager/pull/107) was reviewed with no blocking findings and merged into main after [PR CI 40620906065](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/40620906065) succeeded. Issue #8 closed on merge.
- Source/tag commit: `b838d0692002c42857725eaf740c000ef9f3c6be`. [Main CI 40621198679](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/40621198679) and [Release 40621230598](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/40621230598) succeeded. Release passed Core 15/15, Windows 20/20, Chrome 10/10, ZIP/Setup generation and the seven-frame icon verification.
- [Shortcut Manager v0.1.1](https://github.com/Soki0909/windows-shortcut-manager/releases/tag/v0.1.1) published at 11:31 JST as a normal release. v0.1.0 artifacts/tag were preserved.
- Independently downloaded all four published attachments, matched all three payload hashes against both the published checksum file and GitHub asset digests, verified the Windows ZIP contains the expected EXE/DLL and the extension ZIP has a root manifest with version 0.1.1, then confirmed every downloaded EXE/Setup icon frame matches the original ICO.
- Owner confirmed the v0.1.1 taskbar logo on the local packaged build. Local installed v0.1.0 was not upgraded as part of publication; use the new Setup EXE to update it.

| Published artifact | Bytes | SHA-256 |
|---|---:|---|
| `ShortcutManager-Setup-win-x64-v0.1.1.exe` | 46,626,544 | `fc5888e86fd3e9b293a39c8edc153e401bc8762b46357c99f5851dad3c868d54` |
| `ShortcutManager-win-x64-v0.1.1.zip` | 65,424,881 | `a8c3579c72e8500b2403074d96886b29285d87df290eafa5c51c2f5dca1fb977` |
| `ShortcutManager-TabBridge-v0.1.1.zip` | 9,767 | `41078aebd2feac3a5a656b13ac1c5b262f805a1b7b12d4e46d2fdc8de34d0567` |

### GitHub Release v0.1.0 — published 2026-10-27

- Release: [Shortcut Manager v0.1.0](https://github.com/Soki0909/windows-shortcut-manager/releases/tag/v0.1.0), published at 12:50 JST, normal release (not draft/prerelease).
- Tag source: `7f0201ef875ec3edfcd14c26fd9cb0b7c652e805`, fast-forwarded onto `main` before tagging.
- [CI run 40446133147](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/40446133147) and [Release run 40446184352](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/40446184352) succeeded. Release runner passed Core 15/15, Windows 20/20 and Chrome 10/10, then built ZIPs and the installer with Inno Setup 6.7.3.
- Published attachments: Setup EXE, portable ZIP, TabBridge ZIP and `SHA256SUMS.txt`. Public artifacts were rebuilt on GitHub and have their own hashes; the local-build hashes below are historical local verification evidence.
- Downloaded all four public attachments independently, matched all three payload SHA-256 values against the public checksum file and GitHub asset digests, and verified the Windows executable and extension root manifest are present in their ZIPs.

| Published artifact | Bytes | SHA-256 |
|---|---:|---|
| `ShortcutManager-Setup-win-x64-v0.1.0.exe` | 46,556,702 | `181a39fe1d7a32d5bacc9d6aeba8e4c2aea023231e8a2034cb9204b3323dff20` |
| `ShortcutManager-win-x64-v0.1.0.zip` | 65,293,356 | `08452e8e51e2bb80c3afb8ec126d031fefb5533f9207b54a74864d62263eabd1` |
| `ShortcutManager-TabBridge-v0.1.0.zip` | 9,767 | `968072e14ca2e3931cf14bc9a6b70e4d6e1a8eb85cd4e0e9d76593322badaad0` |

Final local artifacts:

- `artifacts/ShortcutManager-win-x64-v0.1.0.zip` — 63,237,908 bytes — SHA-256 `459ec99bae72f000d9344c786630617e32cd2d51a8bf240e00141f05d99f627f`
- `artifacts/ShortcutManager-TabBridge-v0.1.0.zip` — 9,632 bytes — SHA-256 `4b911cff56d1023beb95b053e19461723291761ced775fb7967cec58798a949e`
- `artifacts/ShortcutManager-Setup-win-x64-v0.1.0.exe` — 46,570,164 bytes — SHA-256 `ce1d40d3560697f200b6a396f753cfaafd080bd5464abe7ee9c767ff66e3cee0`
- `artifacts/SHA256SUMS.txt`

ZIP inspection verified `ShortcutManager.exe` is present (and the old `ShortcutManager.App.exe` name is absent). Extension ZIP has 16 entries, `manifest.json` at root, and no development-only `package.json`.

- GitHub Release artifacts: credential-free generation is implemented locally and in Actions.
- Git remote is `https://github.com/Soki0909/windows-shortcut-manager.git`. After explicit owner authorization, initial commit `b76312d` (`feat: complete Shortcut Manager v0.1.0`) was pushed to `origin/main` on 2026-10-01.
- Chrome Web Store upload/publish: not performed; requires owner developer account, listing, consent, and review.
- Microsoft Store/MSIX: not performed; requires owner account and packaging/signing decisions.
- Authenticode signing: not performed; no certificate was supplied.
- GitHub repository push: completed to `origin/main`. Repository visibility was not changed. GitHub Release v0.1.0 was subsequently published on 2026-10-27 at the owner's explicit request, as recorded above.
- Final audit on 2026-10-01 reran all automated gates successfully, verified both artifact hashes against `SHA256SUMS.txt`, inspected both ZIP structures, matched the packaged scan's exact 19 managed HotKeys, and confirmed zero resident Manager processes.
- Final follow-up audit on 2026-10-27 regenerated both packages from the completed M9/M10 code, independently matched both SHA-256 values, inspected the 404-entry Windows ZIP and 16-entry extension ZIP, and launched the final self-contained GUI. It displayed `Shortcut Manager`, responded, and exited normally within five seconds. All M9/M10 implementation and live gates are complete; commits are pushed to `origin/codex/live-refresh-modern-ui`.
