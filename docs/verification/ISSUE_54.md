# Issue #54 — 配布環境の資料照合と残る実機検証

確認日: 2026-11-05。#54は**部分改善・未完了**。OS/権限/配列/倍率の全実機マトリクスを通過したとは扱わない。

## 公式資料の取得点

- .NET 10の[現行supported-os.json](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.json)を確認。最終変更commitは `44927bc821d37f596e317c4316335632bd38c79b`（2026-09-28T23:10:56Z）。[固定したJSON](https://github.com/dotnet/core/blob/44927bc821d37f596e317c4316335632bd38c79b/release-notes/10.0/supported-os.json)の`channel-version=10.0`、`last-updated=2026-09-28`。
- `families[name=Windows].distributions[id=windows]`にはarchitectures Arm64/x64/x86、最古のWindows clientとして`10-1607-e-lts`がある。`10-1809-e-lts`、`10-21h2-e-lts`も列挙。Home/Proの1607や全Windows 10ではない。Windows 11 Arm64上のx64エミュレーションもnotesへ明記されている。
- [Microsoft LearnのWindows一覧](https://learn.microsoft.com/en-us/dotnet/core/install/windows)もOS/.NETのいずれかのサポート終了で対象外となる条件とWindows 10のエディション制約を示す。[.NET OS lifecycle方針](https://github.com/dotnet/core/blob/main/os-lifecycle-policy.md)と個別OSのlifecycleも再確認する。
- [Inno MinVersion](https://jrsoftware.org/ishelp/topic_setup_minversion.htm)は数値のWindows下限であり、build指定も可能。[Windows数値版の表](https://jrsoftware.org/ishelp/topic_winvernotes.htm)では1507=10.0.10240、1511=10.0.10586、1607/Server2016=10.0.14393。
- 製品の`DialogLayout`も`GetDpiForWindow`を呼ぶ。[MicrosoftのAPI要件](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdpiforwindow)はWindows 10 1607 / Server 2016以降であり、数値下限は現在の呼出しにも整合する。
- [Innoのarchitecture定義](https://jrsoftware.org/ishelp/topic_archidentifiers.htm)では`x64compatible`がx64 OSとWindows 11 Arm64上のx64エミュレーションを含む。`x64os`はエミュレーションを除外する別条件である。

## 限定したSetup変更

旧`MinVersion=10.0`では.NET一覧より古い1507/1511も通るため`10.0.14393`へ修正する。architectureは`x64compatible`、権限は`lowest`のまま。要件のWindows x64、self-contained、通常操作の非管理者原則は変更しない。

14393は必要条件だけであり、1607を含む全SKUのサポートを意味しない。1607 LTSBの通常lifecycleは2026年10月で終了し、ESU等の契約条件や.NET側の扱いは未確認。固定のbuild下限だけで現在のOSサポート可否を判定できない。既存の所有者環境や.NETが示すエミュレーション経路を、未検証という理由だけで切り捨てる条件は追加しない。

## 実行証拠と限界

- 所有者PCを読取り確認: Windows 11 Home / 26H2 / 10.0.26300.9818、OSと確認プロセスはX64、現在のPowerShell tokenは非昇格。ユーザー名/PC名/識別子は収集・公開していない。これは現時点のmetadataであり、全GUI操作や過去のSetup tokenの証明ではない。
- 26H2は[Windows lifecycle](https://learn.microsoft.com/en-us/lifecycle/products/windows-11-home-and-pro)の現行リリースだが、取得した.NETデータは前日の2026-09-28更新で未掲載。所有者PCの実行実績だけから公式.NETサポートを確定せず、候補時の再照合を残す。
- [CI 41893214665](https://github.com/Soki0909/windows-shortcut-manager/actions/runs/41893214665)のdotnet jobはWindows Server 2025 Datacenter 10.0.26100 / image windows-2025-vs2026 20261030.294.1。Core39/Windows208・skip0、実UNC3サイクル、配布/旧Setup更新保持passed。clientのDPI/物理配列/IME/標準ユーザーGUIの証拠ではない。
- 実機のDPI/複数monitor/IME/物理JIS・US/Narrator操作、下限未満OSでのSetup拒否は未実施。[代表組合せ](ENVIRONMENT_MATRIX.md)を未完のまま公開する。

## 今回の最小検証

- GitHub contents APIで固定commitのJSONを再取得し、channel/更新日/Windowsの該当entryを照合。可変mainの内容だけを根拠にしない。
- 実際の`installer/ShortcutManager.iss`を、既存のInno Setup 7.1.1 compiler（`--version`で照合）と52 byteの無害な仮payloadでコンパイルしpassed。出力はgit対象外のcompile-only fixtureに保持し、Setupを実行していない。正式ペイロードのpublish、所有者製品の上書き、ローカルinstaller smoke/lifecycleも行っていない。
- 変更文書の相対リンクと`git diff --check`はpassed。SetupのOS拒否動作は旧OS上で未検証であり、コンパイル成功から実行時の全条件をpassedへ置換しない。

最終候補で同梱runtimeの版・公式対応・lifecycleを再確認し、対象に含める環境の代表シナリオを実施してから#54を完了判定する。
