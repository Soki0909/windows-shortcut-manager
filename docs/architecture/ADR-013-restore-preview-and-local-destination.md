# ADR-013: Confirmed restore plans and local managed destination

Status: Accepted — implements #42/#81/#83/#134 within the owner-authorized v1.0.0 readiness work.

Restoration keeps backup format 1 and config schema/protocol 1. Preview reads bounded, verified archive data without extracting entries or writing config, links, state or backups. Show entry and settings changes, unavailable targets, external key conflicts, source and destination managed folders, and the loss of any unsaved candidate edits on successful restore.

The default is an entire restore which stops on validation failure or external conflict. An explicit alternative preserves all definitions but disables enabled entries with target problems or detected external conflicts. It does not discard entries or hide invalid names, keys, IDs, structure or global format errors. Show each disabled entry and its reason. Re-enabling later requires normal validation.

Bind confirmation to the archive SHA-256, the current config's byte hash, and the exact resulting configuration. Re-scan external keys and revalidate targets before committing. If the source archive, current config, or resulting partial-restore decision changes, stop and require a new preview. Reuse RestoreService and the journaled Apply transaction for ownership checks, conflict checks, current-state backup, commit and rollback. Preview cancellation never calls Apply.

Restore into the caller's current edition-specific managed directory and persist that same destination in restored settings. A backup's former managed-directory setting is displayed as historical data, not used as a later write destination. Neither create nor modify the former directory. Debug always resolves to its isolated directory. The current destination is still subject to existing ownership checks; restoring never authorizes overwriting unmanaged data.

History is a read-only local catalog of the backup directory. Inspect each ZIP independently, retain corrupt entries with errors, distinguish verified automatic receipts from claimed reasons, and support an explicit file picker for manual backups stored elsewhere. Listing does not enforce retention or delete files.

The direct previous-Apply action is available only for an Apply completed during the current Manager session. Associate its backup path and SHA with the successful config snapshot; stop if that snapshot or archive changed, if edits remain unsaved, or if restoration already replaced the state. Earlier sessions and other PCs must use the explicit history/file preview. This avoids guessing that the newest ZIP belongs to the current state. Restoring always creates another current-state backup and does not claim physical-key verification.
