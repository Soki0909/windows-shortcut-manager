# ADR-001: .NET 10 + WPF

Status: Accepted

## Decision
Shortcut Manager v0.1.0 は C# / .NET 10 LTS / WPF で実装する。

## Reasons
- Windows 専用製品。
- Windows Shell / COM と自然に統合。
- Electron より配布サイズ・依存・常駐性を抑えやすい。
- WinUI 3 / Windows App SDK より portable self-contained 配布が単純。
- 本人利用を最短にできる。
- .NET 10 は現在の LTS。

## Consequence
macOS/Linux は対象外。
