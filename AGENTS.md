# AGENTS.md

## Product priority
1. The owner must be able to use the product on their Windows machine.
2. Do not sacrifice rollback/safety.
3. Keep GitHub build/release reproducible.
4. Generalize only where it does not delay or destabilize the above.

## Source of truth
Read `docs/requirements/` before `docs/design/`.
Requirements are user-owned. Do not silently change them.
Follow accepted ADRs in `docs/architecture/`.

## Architecture invariants
- Shortcut Manager is not resident.
- No global keyboard hook in the Manager.
- Windows `.lnk` HotKey is the runtime trigger.
- BAT remains an external BAT/CMD file.
- Chrome Tab Group uses the dispatcher URI protocol.
- No Native Messaging, localhost daemon, Named Pipe daemon, or Chrome Commands in v0.1.0.
- No host permissions/content scripts in the extension.
- Do not hardcode personal group names or Extension ID into product code.

## Implementation practice
For the v0.1.0 multi-step implementation, maintain
`docs/implementation/PLAN_v0.1.0.md`.
Update Progress, Discoveries, Decisions, and Validation Evidence as work proceeds.

Do not stop at scaffolding.
After each milestone, run its validation before moving on.
When a test fails, fix it before starting unrelated work.

Minimize dependencies.
Do not add a NuGet/npm package if the standard platform can reasonably do the job.

## Safety
Never delete or overwrite unmanaged user shortcuts.
Honor the managed-folder ownership marker.
Backup before destructive Apply/Restore.
Do not commit secrets, signing keys, Store credentials, or user-specific absolute paths.

## Development and installed product

- The owner uses the installed product for daily work. Use `scripts/run-debug.ps1` for development UI checks; do not launch repository Release/publish outputs as the ordinary development app.
- Debug must keep its separate executable name, `[DEBUG]` title, D badge, taskbar identity, LocalAppData and managed-shortcut directory. Never automatically load or copy installed-product config into Debug. A copied config must not redirect Debug writes into the product's managed folder.
- Treat the other edition's HotKeys as external conflicts for Apply, startup audit and Restore. Never clear or overwrite them to make a debug test work.
- Building/debugging does not authorize replacing the installed product. Update it with a release installer only when the owner requests an installed-product update. Package only Release builds.

## Completion
Do not claim v0.1.0 complete until `docs/verification/VERIFICATION.md`
contains the executed build/test/package evidence and the remaining manual/live checks are either passed or explicitly blocked by an external prerequisite.
