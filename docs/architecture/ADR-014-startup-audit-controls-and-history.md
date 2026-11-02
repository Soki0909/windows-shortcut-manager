# ADR-014: Startup audit authority and local history

Status: Accepted — implements #43/#44 in the owner-authorized v1.0.0 readiness work.

Keep config schema/protocol 1, the nonresident architecture, and the default automatic repair of safely verifiable managed-link drift. Store audit preferences and history separately in the current edition's data directory; never import product preferences into Debug.

Offer a persistent audit-only preference, and `--audit-only` for one GUI launch. Setup provides a separate Start Menu shortcut without a HotKey. A GUI audit-only launch skips both automatic reconciliation and automatic pending-transaction recovery. It still reads and reports differences; the user can separately authorize explicit saved-link repair or journal recovery. Command-mode mutations remain explicit operations and cannot be combined with `--audit-only`.

Audit-only is a policy on automatic mutation, not a claim that no local diagnostic metadata is written. Config, managed links, state and backups stay unchanged until an explicit mutation is selected. Missing/corrupt/unsupported audit-policy records fail to audit-only when their contents cannot be interpreted; a genuinely absent policy uses the existing automatic default.

Do not repair automatically when targets cannot be validated, known external keys conflict, or the external scan is incomplete. Record an intent fingerprint before an automatic repair. Include canonical config and actual field-level differences so identical attempts do not repeatedly generate backups after failure or interruption. Corrupt attempt records or an inability to persist intent also hold automatic repair. Explicit repair may proceed after the user reviews the details and incomplete-scan notice; ownership, known conflicts, validation, backup and Apply journal gates still apply.

Recheck the saved-config source after scanning and before repair. Explicit repair uses the saved config and preserves an unsaved candidate. It never adopts external link changes into config. A successful Apply transaction remains successful even if a separate post-repair audit or history write fails; distinguish that from verified matching links and leave physical HotKey status unconfirmed.

Record field-level actual/expected values, mode, result, before/after differences and pre-repair backup locally. Read history records independently with size/structure limits, show the latest 50, and retain corrupt records as error rows. History is not automatically deleted or transmitted. The detail view leads to recovery help and the existing confirmed restore preview. It never restores by merely opening a record.
