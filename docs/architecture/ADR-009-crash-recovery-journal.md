# ADR-009: Durable Apply snapshots before mutation

Status: Accepted — implementation of owner-authorized safety release issues #3/#63.

- Keep the existing Apply mutex, backup and staged-link validation. Before config/link mutation, flush bounded snapshots and an immutable journal containing exact destinations, before/after hashes and snapshot names. Record per-file/config/state checkpoints for isolated process-kill tests.
- GUI startup recovers pending transactions before loading config or auditing links. Repeated recovery always copies snapshots; it never consumes the only original. The Apply entry point refuses pending transactions until startup recovery has run. An abandoned mutex grants ownership; release it in finally and inspect the journal before writing.
- Flush a terminal marker only after a complete new or restored state. Terminal transactions may be cleaned without changing product files. Before any recovery write, validate all snapshots and current destinations. Protect unexpected files and post-interruption edits by stopping with retained evidence. Older recovery.json-only transactions need manual verification.
- Per-config-path process leases prevent two current-edition windows/commands from retaining stale candidate settings. Debug and installed product remain separate. This does not claim coordination with old binaries that predate leases; close old Manager before updating.
- File data and journal use Flush(true), WriteThrough and same-directory atomic replacement. Process-kill testing covers application interruption; physical power-loss hardware/cache behavior is not simulated. Missing/corrupt recovery sources stop automatic writes instead of guessing.
- No resident process, hook, daemon, product dependency, or change to HotKey execution is introduced.
