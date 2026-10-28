# Shortcut Manager logo

Issue: [#8](https://github.com/Soki0909/windows-shortcut-manager/issues/8)

The product owner supplied these assets on 2026-10-28 and requested their use
as the Shortcut Manager logo. This records the source and authorization for
this integration; it does not assert a separate third-party license or an
independent copyright clearance.

- `shortcut_manager_logo.svg`: editable vector source.
- `shortcut_manager_logo_<size>.png`: original transparent PNG exports,
  16, 24, 32, 48, 64, 128, 256, 512 and 1024 pixels.
- `../../src/ShortcutManager.App/Assets/shortcut_manager_logo.ico`: original
  Windows ICO with 16, 24, 32, 48, 64, 128 and 256 pixel frames.
- `../../src/ShortcutManager.App/Assets/shortcut_manager_logo_64.png`: unchanged
  copy of the 64 pixel export for the WPF header.

The application project embeds the ICO in the EXE and includes both runtime
assets as WPF resources. MainWindow explicitly uses the ICO for its title bar,
taskbar and Alt+Tab identity. Inno Setup uses the same ICO for Setup; installed
Start-menu/desktop launchers and the uninstall entry use the EXE's icon.

Keep the original exports intact. Changing the artwork requires replacing the
runtime copies as well. No image-generation or conversion dependency is needed.

After packaging, run `./scripts/verify-branding.ps1 -Version 0.1.1` on Windows.
It reads EXE and Setup resources without executing either binary, checks every
embedded icon frame against the original ICO, and verifies artifact checksums.
CI and Release run this check after building the installer.
