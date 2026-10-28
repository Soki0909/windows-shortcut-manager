# ADR-007: Separate development builds from the installed product

Status: Accepted — owner requested daily use of the installed product and separate debug verification on 2026-10-28.

- Release keeps the existing executable, install path, Shell identity, LocalAppData data path and managed Start-menu folder. A new build does not replace the installed product; updating it requires running a release installer.
- Debug builds are named `ShortcutManager.Debug.exe`. They display `[DEBUG]` in the title/header/About and a generated amber D badge on the Window/header icon. Their explicit `ShortcutManager.Desktop.Debug` AppUserModelID keeps their taskbar group separate; set it before showing any UI, following [Microsoft's API contract](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-setcurrentprocessexplicitappusermodelid).
- Debug uses `%LOCALAPPDATA%\ShortcutManager.Debug` for config/state/logs/backups/staging and a separate `Programs\Shortcut Manager Debug` managed folder. A config-specified managed-directory override is ignored by Debug, including Apply, startup audit and Restore. Production data is never copied into Debug automatically.
- Both editions still use Windows `.lnk` HotKeys, so the Windows key namespace is shared. Scans treat the other edition's links as external conflicts. Restore passes scanned external HotKeys to the same Apply transaction, preventing a restored debug backup from taking a product HotKey.
- `scripts/run-debug.ps1` builds and launches the exact Debug EXE. Packaging accepts only Release; installer/release artifacts remain product builds. Neither edition becomes resident.
- Full-DPI and pinned-taskbar matrices remain separate manual checks. This change does not alter user-owned requirements, install a debug edition, update an existing product install, or republish v0.1.1.
