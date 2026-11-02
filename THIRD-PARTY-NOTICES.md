# 利用条件と第三者の著作物

Shortcut Managerのソースコードは [MIT License](LICENSE) で提供します。
2026-11-02に所有者の「推奨のものでよい」という選択に基づいて採用しました。
MITの条件は著作権表示・許諾文の保持です。第三者の著作物には各提供元の条件が適用されます。

## 製品に同梱するもの

| 対象 | 条件・出所 | 配布時の表記 |
| --- | --- | --- |
| Shortcut Manager / Tab Bridge | MIT、Shortcut Manager contributors | `LICENSE` |
| .NET self-contained runtime | Microsoft .NET、MITと第三者コンポーネントの条件 | Windows配布物の `licenses/` に、実際に復元したruntimeパッケージのLICENSE/NOTICESをそのまま収録 |
| WPF / Windows Desktop runtime | Microsoft .NET WPF、MITと各コンポーネントの条件 | 同上。ライセンス文はruntimeパッケージに付属するものを正本とする |
| 製品ロゴ | 所有者提供、製品への組込みを所有者承認済み | `BRANDING.md`（元文書: `assets/branding/README.md`）。第三者権利の独立した調査済みという意味ではない |

Tab Bridgeにはnpmパッケージや.NET runtimeを同梱しません。ChromeのAPI・WindowsのCOM/APIは各プラットフォームから提供され、製品が再配布するライブラリではありません。

## ビルド・テストに使うもの

- .NET SDK: Microsoft、MITほか。製品にはSDK全体を含めません。
- xUnit / xunit.runner.visualstudio: Apache-2.0。テスト専用で製品配布物には含めません。
- Microsoft.NET.Test.Sdk: Microsoft、MIT。テスト専用。
- Node.js: MITおよび同梱第三者の条件。拡張テスト専用、製品には含めません。
- Inno Setup: JRSoftwareのInno Setup License。生成したSetupの条件はInno Setupの許諾に従います。コンパイラは製品ZIPに含めません。
- GitHub Actions: 各ActionのリポジトリのLICENSEに従います。製品には含めません。

追加依存を導入する場合はライセンスと再配布物を再確認し、この表と配布時の表記を更新してください。Windows配布物に実runtimeのライセンス文がない場合はパッケージ作成を失敗させます。

参照元: [MIT](https://opensource.org/license/mit)、[xUnit](https://github.com/xunit/xunit/blob/main/LICENSE)、[VS runner](https://github.com/xunit/visualstudio.xunit/blob/main/License.txt)、[Test SDK](https://github.com/microsoft/vstest/blob/main/LICENSE)、[Inno Setup](https://jrsoftware.org/files/is/license.txt)。配布runtimeの正本は同梱した版の原文です。
