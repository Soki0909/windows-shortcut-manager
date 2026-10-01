# v0.1.0 Verification

Updated: 2026-10-01 (Asia/Tokyo)

## Environment

- Windows x64 target machine
- .NET SDK `10.0.401`
- Node.js `v22.15.0`
- Chrome: `C:\Program Files\Google\Chrome\Application\chrome.exe`
- Product version / config schema / Dispatcher protocol: `0.1.0 / 1 / 1`

## Automated verification

| Area | Command | Result |
|---|---|---|
| Core + Windows | `dotnet test ShortcutManager.sln -c Release --no-restore` | Core 15/15, Windows 11/11 passed |
| Extension | `node --test tests/ChromeExtension.Tests/protocol.test.js` | 10/10 passed |
| Version/policy | `scripts/verify-version.ps1 -ExpectedVersion 0.1.0` | Passed; MV3, no commands/host_permissions/content_scripts/externally_connectable |
| Release build | `dotnet build ShortcutManager.sln -c Release --no-restore` | Passed with 0 warnings / 0 errors |
| Packaging | `scripts/package.ps1 -Version 0.1.0` | Passed; self-contained EXE and root manifest verified |
| Packaged GUI smoke | Start self-contained EXE, wait for window, request normal close | `Shortcut Manager` window appeared within 1 second; responding; clean exit; 0 remaining processes |

Tests cover config roundtrip/polymorphism, Unicode, schema/URL/Extension ID validation, disabled invalid target behavior, GUID/HotKey conflicts, filename sanitation, Dispatcher percent encoding, real WScript `.lnk` roundtrip, `.url` numeric HotKey decoding (`1602 → CTRL+ALT+B`), scan root dedupe, managed ownership rejection, staging/read-back/backup/commit, idempotence, external conflict blocking, forced rollback, Restore through the Apply transaction, and Chrome protocol/group/tab selection.

## 2026-10-01 live-refresh and UI follow-up

- Apply now reports changed shortcut lifecycle events to Explorer synchronously: deletion for links moved out of service, then creation/update for committed links, plus the managed-directory update. Rollback emits the matching reverse lifecycle. Windows regression tests verify replacement, removal, and rollback notification order.
- The packaged native GUI loaded the current host configuration and displayed three physical keyboard rows in `QWERTYUIOP / ASDFGHJKL / ZXCVBNM` order. The refreshed card, input, table, status, and action hierarchy were inspected in the running Windows application; opening the Q editor preserved the existing data.
- The current configuration was reapplied from the packaged GUI. The application reported `変更を反映しました。Windows の再起動は不要です。` without requiring a Windows restart.
- A temporary `F=一時メモ帳テスト` application entry targeting `C:\Windows\System32\notepad.exe` was added and applied successfully. The physical `CTRL+ALT+F` before-restart check remains pending owner confirmation. The entry will be removed and reapplied immediately after that confirmation; until then this is not recorded as a passed live HotKey gate.
- Final automated follow-up results: Core 15/15, Windows 11/11, Chrome 10/10, Release build 0 warnings/errors, version/policy check passed, and both release packages were regenerated successfully.

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
- Native Windows UI inspection now passes for the modern layout and edit flow. The remaining M9 manual gate is owner confirmation that the newly applied temporary `CTRL+ALT+F` opens Notepad before any restart, followed by removal of that temporary entry.

## Publication status

Final local artifacts:

- `artifacts/ShortcutManager-win-x64-v0.1.0.zip` — 63,191,398 bytes — SHA-256 `d35ee2507d7c83432dd7c4674136e1dc56b9eec723f8ca9bb95596b37572b8dc`
- `artifacts/ShortcutManager-TabBridge-v0.1.0.zip` — 9,632 bytes — SHA-256 `4b911cff56d1023beb95b053e19461723291761ced775fb7967cec58798a949e`
- `artifacts/SHA256SUMS.txt`

ZIP inspection verified `ShortcutManager.exe` is present (and the old `ShortcutManager.App.exe` name is absent). Extension ZIP has 16 entries, `manifest.json` at root, and no development-only `package.json`.

- GitHub Release artifacts: credential-free generation is implemented locally and in Actions.
- Git remote is `https://github.com/Soki0909/windows-shortcut-manager.git`. After explicit owner authorization, initial commit `b76312d` (`feat: complete Shortcut Manager v0.1.0`) was pushed to `origin/main` on 2026-10-01.
- Chrome Web Store upload/publish: not performed; requires owner developer account, listing, consent, and review.
- Microsoft Store/MSIX: not performed; requires owner account and packaging/signing decisions.
- Authenticode signing: not performed; no certificate was supplied.
- GitHub repository push: completed to `origin/main`. Repository visibility was not changed and a GitHub Release was not created; both remain owner-controlled.
- Final audit on 2026-10-01 reran all automated gates successfully, verified both artifact hashes against `SHA256SUMS.txt`, inspected both ZIP structures, matched the packaged scan's exact 19 managed HotKeys, and confirmed zero resident Manager processes.
