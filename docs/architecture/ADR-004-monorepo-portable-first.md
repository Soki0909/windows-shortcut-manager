# ADR-004: Monorepo + Portable-first distribution

Status: Accepted

## Decision
Windows App と Chrome Extension を同一 GitHub repository で管理。
初期 Windows 配布は self-contained folder ZIP。
Chrome public distribution は Chrome Web Store。

## Reasons
- protocol version を1箇所で変更・検証しやすい。
- 同一 release version を保ちやすい。
- GitHub Actions を共有できる。
- installer/signing を v0.1.0 blocker にしない。
