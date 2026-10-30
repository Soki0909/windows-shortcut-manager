# Shortcut Manager Tab Bridge

Windows Shortcut Manager が起動する汎用 Dispatcher URL から、現在開いている同名の Chrome タブグループを展開・前面化します。データはローカルにのみ保存され、host permission、content script、Chrome Commands は使用しません。

Protocol v1の新action `toggle-group`は、前面アプリに関係なくcollapsedを反転します。折りたたみはタブ削除ではありません。旧`focus-group`とPopupの「開く」は常に展開します。新Managerは起動監査／Applyでtoggle-groupへリンクを再生成するため、この対応拡張を先に導入してください。元の前面条件はP3の#112へ分離済みです。

開発時は `chrome://extensions` でデベロッパーモードを有効にし、このディレクトリを「パッケージ化されていない拡張機能」として読み込みます。Popup に表示される Extension ID を Shortcut Manager に設定してください。

