# ADR-002: Windows Shell `.lnk` owns global HotKeys

Status: Accepted / Windows実機検証済み

## Decision
Shortcut Manager は global keyboard hook を持たず、Start Menu 配下の `.lnk` HotKey を生成する。

## Evidence
2026-09-30 Windows 実機 PoC:
`Ctrl+Alt+R → .lnk → chrome.exe → extension dispatcher → 研究`
成功。

## Consequence
Manager process は通常時不要。
runtime RegisterHotKey 全列挙は行わない。
