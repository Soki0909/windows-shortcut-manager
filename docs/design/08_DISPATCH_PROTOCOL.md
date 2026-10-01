# 08. Dispatcher Protocol v1

## 1. Purpose

Windows Shortcut Manager と Chrome Extension の結合点を最小化する。

Product version と独立した互換契約。

Protocol version:
`1`

## 2. URI

Canonical:

```text
chrome-extension://{extensionId}/dispatch.html?v=1&action=focus-group&group={percentEncodedGroupName}
```

Parameter order も上記を canonical とする。

## 3. Windows builder

C#:

```csharp
Uri BuildFocusGroupUri(string extensionId, string groupName)
```

Validation:
- extension ID regex `^[a-p]{32}$`
- groupName trim 1..200
- percent encoding は .NET URI/query-safe method を使用
- string concatenation で raw groupName を直接入れない

## 4. Extension parser

Required:
- `v`
- `action`
- `group`

Accepted:
- `v=1`
- `action=focus-group`

Unknown extra parameters:
ignore。

Unknown version/action:
fail closed。

## 5. Unicode

`URLSearchParams` で decode。
decode 後 NFKC normalize は group matcher 側。

Examples:
- `研究`
- `7脱`
- spaces
- emoji

を unit test に含める。

## 6. Error codes

- `invalid-protocol`
- `invalid-action`
- `empty-group-name`
- `group-not-found`
- `group-query-failed`
- `group-expand-failed`
- `window-focus-failed`
- `tab-query-failed`
- `tab-activate-failed`
- `internal-error`

## 7. Compatibility

Shortcut Manager が protocol v1 URL を生成する限り、
Extension product version が更新されても v1 contract を壊さない。

Breaking change:
- v2 parameter
- Manager と Extension は v1/v2 coexist period を設ける

## 8. No callback

v1 は one-way launch。
Windows app へ callback を返さない。

理由:
- Native Messaging / localhost 等を不要に保つ
- HotKey runtime に Manager process が存在しない

Manager Test は「launch succeeded to Chrome」まで。
Chrome 内の success は Extension UI/behavior が verification surface。
