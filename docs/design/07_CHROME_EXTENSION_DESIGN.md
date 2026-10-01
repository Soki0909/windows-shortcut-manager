# 07. Chrome Extension Detailed Design

## 1. Product

Display name:
`Shortcut Manager Tab Bridge`

Manifest:
- MV3
- background service worker
- `type: "module"`

## 2. No build step

Source tree がそのまま extension package。

利点:
- GitHub diff と配布物の差を最小化
- npm supply-chain 依存を持たない
- Chrome Web Store ZIP を単純生成

Node は tests 用のみ。

## 3. Manifest permissions

```json
"permissions": [
  "tabGroups",
  "tabs",
  "storage",
  "notifications"
]
```

禁止:
- host_permissions
- content_scripts
- commands
- externally_connectable
- remote code

## 4. Files

### `lib/normalize.js`
pure:
- `normalizeGroupTitle(value)`

### `lib/dispatch-protocol.js`
pure:
- parse query
- validate v/action/group
- result codes

### `lib/group-selection.js`
pure:
- duplicate group selection policy

### `lib/tab-selection.js`
pure:
- remembered tab selection policy

### `service-worker.js`
Chrome API integration.

### `dispatch.js`
query parse → runtime message → status → close self.

### `popup.js`
list groups / open selected / collapse all / settings.

## 5. Runtime message

Dispatcher → Worker:

```json
{
  "type": "dispatch",
  "protocolVersion": 1,
  "action": "focus-group",
  "payload": {
    "groupName": "研究"
  }
}
```

Worker → Dispatcher:

success:
```json
{
  "ok": true,
  "code": "ok",
  "groupTitle": "研究",
  "groupId": 1,
  "windowId": 2,
  "tabId": 3
}
```

failure:
```json
{
  "ok": false,
  "code": "group-not-found",
  "message": "..."
}
```

IDs は response/debug 用であり永続化しない。

## 6. Group selection

normalize:
`trim().normalize("NFKC")`

matching:
exact normalized title.

0:
not-found.

1:
select.

2+:
- get last focused normal window
- same window match preferred
- otherwise deterministic first sorted by `windowId`, then `id`

## 7. Focus flow

1. query group
2. `tabGroups.update(id,{collapsed:false})`
3. `windows.update(windowId,{focused:true})`
4. query tabs in group
5. select remembered tab
6. `tabs.update(tabId,{active:true})`
7. remember selected tab
8. result return

If group expansion succeeds but tab activation fails:
- return `tab-activate-failed`
- do not claim full success

## 8. Last tab storage

Key:
`lastTabByGroupTitle`

Value:
- `tabId`
- `url`
- `savedAt`

No sync storage.
No external upload.

Retention:
group title entries can remain; optional cleanup when >100 entries.

## 9. Startup collapse

Settings:

```json
{
  "settings": {
    "collapseOnStartup": false,
    "startupCollapseWindowMs": 20000,
    "debugMode": false
  }
}
```

Public default = false.
本人 = popup から true にする。

onStartup:
- set deadline
- collapse all existing groups

onCreated within deadline:
- collapse created group

## 10. Popup exact UI

Header:
- name
- extension version
- extension ID
- protocol `v1`

Settings:
- Collapse groups on Chrome startup [toggle]

Actions:
- Refresh
- Collapse All

Group list:
- title
- collapsed / expanded
- button `Open`

No hardcoded Research / 7脱 buttons.

## 11. Dispatcher behavior

Visible status:
- `Opening "研究"...`
- failure code if failed

Success:
- close tab immediately after result.

Failure:
- notification create
- keep Dispatcher visible ~1.5s for debugging/user recognition
- close

Debug mode:
- do not auto-close failure page
- show structured result.

## 12. Extension ID

Code does not know its own expected ID.
Popup displays `chrome.runtime.id`.

Shortcut Manager stores the ID.

After CWS release:
Store ID becomes distribution default in Windows app resource.

## 13. Web Store readiness

Package ZIP root:
- manifest.json
- all source files
- icons

No source maps/build artifacts required.

Permissions explanation documented in README.

Privacy:
- local-only
- no server
- no analytics
- no content script
