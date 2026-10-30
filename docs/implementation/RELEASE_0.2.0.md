# v0.2.0 safety and Chrome toggle release

## Scope and priority

Include merged #110/#2/#4/#5/#6/#7/#37/#38/#39/#40. Finish the remaining P0 crash recovery #3 and its abandoned-mutex dependency #63 first, then stale-window #34 and busy-state #35. Finish integration evidence #57, compatibility guidance #92 and the publication gate #61 before tagging. This is an incremental release in the existing unsigned/unpacked distribution channel; broad onboarding, signing credentials and P3 research are separate backlog items.

## Progress

- [x] #3/#63: durable snapshots and startup recovery; real child-process kill checkpoints, repeated recovery and abandoned ownership tests.
- [x] #34/#35: per-edition instance lease, ViewModel update guards and disabled controls while busy.
- [x] #92: minimum-extension query, protocol diagnostics, Settings/Popup guidance and release compatibility table.
- [x] #57: rerun the dedicated real Chrome suite against this version.
- [x] #61: draft-download verification and disposable-runner Setup smoke are required before publication. CI run 41004714567 passed local-artifact Setup installation, installed CLI validation and uninstall. The tagged workflow repeats this against downloaded draft assets.
- Publication status is tracked by [PR #113](https://github.com/Soki0909/windows-shortcut-manager/pull/113) and the [v0.2.0 Release workflow](https://github.com/Soki0909/windows-shortcut-manager/actions/workflows/release.yml). Published asset verification is a gate before the release becomes public.

## Discoveries and decisions

- The previous release workflow published immediately and used wildcard assets, which could collect stale local versions. The new path names exactly four assets, creates a draft and downloads them for revalidation. Published assets are immutable; incomplete draft reruns fail without overwriting.
- Existing before/after rollback only held prior config in memory. Snapshot files now precede the first write. A terminal marker separates cleanup from recoverable transactions.
- Requirement source remains docs/requirements; no schema version change. Protocol v1 retains focus-group. New toggle links request extension 0.2.0; old app/new extension remains compatible. Manager cannot inspect the installed extension version through the one-way protocol.

## Validation evidence

- Initial process-kill and busy regression: Windows 65/65 passed, including 10 kill checkpoints and a second kill during recovery. Core baseline 27/27, extension baseline 12/12.
- Final evidence is updated after the versioned build and CI.

- Versioned Release: Core 27/27, Windows 68/68, Node 13/13 passed. Local ZIP/Setup, branding, signature policy (NotSigned) and hashes passed; tampered checksum rejected. Debug Settings compatibility text rendered within its window. Test-only process closed without config edits.
- Real Chrome run chrome-live-1793334818957: 16/16 including cleanup passed; physical keys remain covered by prior 22-record owner run.

- Final real Chrome run chrome-live-1793335333677: 17/17 passed, including explicit page/Worker unhandled-exception observers, cold startup and cleanup. During the earlier rerun an open Debug UI correctly blocked CLI scan; the owned test process was closed before rerunning.

- CI 41004714567 passed all gates on a clean Windows runner after registering CrashDriver in the solution. An initial --no-build run exposed the missing fixture build; the fixed solution build/test path passed locally and in CI.

## Final publication version: v0.2.1

- The v0.2.0 Release run 41005299163 was cancelled before draft creation. Keep its source tag immutable; publish the patch as v0.2.1.
- Cleanup now removes journal intent while the terminal marker still exists, before recursively deleting snapshots. A kill at cleanup-intent-removed is the eleventh checkpoint. Windows 69/69 passed; Core 27 and Node 13 remain unchanged. The final PR/Release workflows re-run all gates.
- v0.2.1 notes: [update instructions](../releases/v0.2.1.md). Personal installed product is not changed by publishing.
