# Shortcut Manager Tab Bridge

Windows Shortcut Manager が起動する汎用 Dispatcher URL から、現在開いている同名の Chrome タブグループを展開・前面化します。データはローカルにのみ保存され、host permission、content script、Chrome Commands は使用しません。

開発時は `chrome://extensions` でデベロッパーモードを有効にし、このディレクトリを「パッケージ化されていない拡張機能」として読み込みます。Popup に表示される Extension ID を Shortcut Manager に設定してください。

