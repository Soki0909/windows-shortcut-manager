# ADR-011: Config compatibility and downgrade protection

Status: Accepted — implementation of #53 in the owner-authorized v1.0.0 readiness work.

## Current persisted formats

All published v0.1.0–v0.3.1 versions use `schemaVersion=1` and `dispatchProtocolVersion=1`. v1.0.0 retains both. Product SemVer, config schema, backup format and dispatch protocol are separate contracts. A product version bump alone never rewrites config. Known numeric enum values remain readable; unknown fields/types are rejected rather than silently discarded.

Read the version header before polymorphic deserialization. An incompatible numeric version (including fractions, exponent notation and values outside Int32) is a distinct error even when a future config contains unknown target types or members. Bound the displayed numeric text. Reject it on Load, Apply, Restore, SaveAtomic and recovery initialization. Startup audit must not run with an incompatible config. Do not label it corrupt, copy it to a `.broken` file, or initialize it as empty. Offer a diagnostic directing the user to a compatible newer application.

## Future schema migration contract

Do not invent a historical schema or convert a valid schema-1 file for this release: no earlier persisted schema exists. Before accepting an actual schema 2, add and test an explicit sequential `1 → 2` migration; each later transition must have its own conversion. Required gates for each transition:

1. Read a bounded, immutable snapshot and verify the source version. Refuse unknown/newer versions and missing transition paths.
2. Save a verified pre-migration backup of config, state and managed links. Retention must protect the rollback copy. No mutation if backup creation fails.
3. Transform in memory through each adjacent supported version; validate each result and the complete destination. Never migrate by deserializing with unknown-member skipping.
4. Commit only through the existing journaled Apply path and verify all persisted results before its terminal marker. Migration failure/interruption restores original config/link/state bytes; preserve journal and backup if rollback cannot finish.
5. Add fixture tests for every supported source version, chained transitions, corrupt/unknown input, backup failure, interruption and repeated recovery before publishing any new schema.

The current release has no data conversion; it closes the unsafe path that allowed command-mode Apply/Restore to overwrite an already newer version with schema 1.

## Downgrade / update rollback

When the schema/protocol are still 1, a prior compatible app can read the same config. Close both editions before using the earlier official Setup; preserve the current installation and data first. The earlier app may have fewer safety fixes; do not claim every historical version is safe to downgrade to.

When a future schema differs, install a compatible newer app instead of retrying Apply or initialization in an old app. To deliberately revert to an older schema, use that newer app's documented, explicit migration rollback restoring its pre-migration snapshot, then run the prior Setup. If the newer app is unavailable, preserve all current data and recover a known compatible backup into an isolated environment before any manual replacement. Do not hand-edit just the version field; that does not convert the content.

Existing malformed JSON can still be recovered by explicitly backed-up initialization or a validated Restore. Version-compatible recovery never changes unmanaged files or suppresses external HotKey conflicts. No dependency or resident process is added.
