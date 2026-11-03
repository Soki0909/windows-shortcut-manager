# Windowsコントラスト配色 / #21/#155

配色はWindowsの高コントラスト設定に追従する。アプリ独自の設定やconfig schemaは追加しない。色を意味別のresourceにまとめ、XAML/エラー欄/一覧からDynamicResourceで参照する。

| 用途 | 高コントラスト時の組 |
|---|---|
| ウィンドウ、カード、状態欄、キー枠 | Window / WindowText |
| 主操作、hover、選択 | Highlight / HighlightText |
| 列見出し、標準コントロール | Control / ControlText |
| メニュー | Menu / MenuText |
| 無効な操作 | Window / GrayText、背景・文字を薄くしない |
| エラー、警告、保存状態 | WindowText、理由や「要確認」「無効」「未反映」等の文字も保持 |

通常配色では従来の色を基本にし、警告文字を読みやすくする。固定色を残すのはDebugアイコンの素材等、意味別UI配色ではない部分のみ。高コントラストを単に黒背景/白文字へ固定せず、ユーザーが選んだWindowsの色の組を利用する。

起動時に設定を読み、SystemParameters.StaticPropertyChangedと各WindowのWM_SYSCOLORCHANGE後に再評価する。変更をUI dispatcherへまとめ、既に開いている画面のresourceを置き換える。終了時はstaticイベントの購読を解除する。OSの設定を変更しない。表示中Windowの通知であり、常駐やグローバル入力hookは追加しない。

派生Windowには共通Window styleを明示し、黒文字等の既定値が残らないようにする。起動診断も共通の配色/サイズ制限を使う。一覧は通常行と選択セルの文字・背景の両方を指定する。

## 主操作hoverの修正

#155では基底ButtonのControlTemplateが薄いhover背景を直接設定し、主操作の白文字と組み合わさっていた。hoverをstyle側の意味別resourceへ移し、有効な場合だけ適用する。主操作styleは前景と背景を同時に指定し、templateが別の背景へ上書きしないようにする。

## 検証と限界

通常/明暗の高コントラストで前景/背景の組とコントラスト比を確認する。実Appを参照するWPFテストは、元のXAMLのIsMouseOver条件だけをfixture用Tagへ置き換え、OS入力なしで同じstyle/template優先順位を評価する。読取り専用IsMouseOverの変更やprivate keyの書換えは行わない。

同じコントロールを通常→明暗高コントラスト→通常へ更新し、hover/disabled/エラー/派生Windowの実効色と、現在のOS配色への更新・Dispose後に更新しないことを確認する。WindowsテストはコピーしたViewModelソースではなく実Appアセンブリを参照する。

描画fixtureはWindows設定を変えずに色データを注入するため、ネイティブの全メニュー/Popup/ComboBoxテーマや実Narrator操作を完全再現したものではない。Windowsの実コントラストテーマでの一連操作は#21/#54の未完了条件として残す。

参考: [WPFのシステムresource](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/systems/xaml-resources-how-to-use-system)、[動的resource](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/xaml-resources-define)、[Windows高コントラストの色の組](https://learn.microsoft.com/en-us/windows/win32/winauto/high-contrast-parameter)。
