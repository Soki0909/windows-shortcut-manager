# ADR-005: Config in LocalAppData

Status: Accepted

## Decision
`config.json` は `%LOCALAPPDATA%\ShortcutManager`。

## Reason
Application executable paths / source shortcut paths are machine-specific。
Roaming profile へ自動移動させるより explicit backup/restore の方が安全。
