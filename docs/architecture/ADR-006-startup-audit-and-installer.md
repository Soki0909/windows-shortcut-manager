# ADR-006: Startup audit and per-user installer

Status: Accepted — owner requested startup audit/repair and installation on 2026-10-27.

- Keep config.json in LocalAppData as the source of truth. On GUI startup, compare all enabled managed links with resolved target, arguments, working directory, HotKey, icon, description and window style; detect missing and obsolete links.
- Repair differences through the existing backup/staging/rollback Apply transaction. Skip writes when consistent. Stop on invalid config, external HotKey conflict, missing ownership marker in a nonempty folder, or unexpected files/subfolders. Do not import external changes into config silently.
- Explorer's active registration table cannot be enumerated through a documented API. Audit verifies persisted shortcuts; after repair offer the established consent-based Explorer restart. Do not restart Explorer on every launch.
- Add a conventional per-user Inno Setup installer alongside the portable ZIP. This extends ADR-004 in response to the owner's request; it does not introduce an updater, resident service, or custom installer framework.
- Install self-contained binaries to LocalAppData/Programs/Shortcut Manager, create an application launcher outside the managed HotKey folder, and register uninstall with Windows. Reinstall/upgrade uses the same AppId and preserves config/backups/managed links. Uninstall removes installed application files and its launcher, preserving user data and working HotKeys.
- Inno Setup is a pinned, checksum/signature-verified build tool only; no new application NuGet/npm dependencies. Signing remains a separate release step.
