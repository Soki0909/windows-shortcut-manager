# 10. Test / Verification Strategy

## 1. Rule

「build succeeded」だけで完成扱いしない。

各 milestone:
- smallest relevant test
- component test
- full test gate

の順で回す。

Validation failure を残して次 milestone へ進まない。

## 2. Core unit tests

必須:

### Config
- round-trip all target types
- Unicode group name
- invalid schema
- duplicate GUID
- duplicate HotKey
- invalid Website scheme
- invalid extension ID

### HotKey
- A-Z
- Ctrl+Alt canonical
- decode `.url` numeric
- `1602 → Ctrl+Alt+B`

### Filename
- invalid Windows chars
- Unicode
- reserved filename
- long name truncation

### Dispatcher URI
- 研究
- 7脱
- spaces
- emoji
- percent encoding
- invalid extension ID

### Validation
- missing target
- disabled shortcut does not reserve generated output
- active duplicate blocks Apply

## 3. Windows integration tests

Windows runner / local Windows:

- create `.lnk` in temp
- read back target/args/hotkey
- Website spec roundtrip
- BAT spec roundtrip
- Chrome spec roundtrip
- `.url` parser fixture
- inventory root dedupe
- marker ownership behavior
- staged apply success
- forced failure rollback
- unmanaged nonempty folder protection
- backup create / restore

Tests never modify real user Start Menu except explicitly marked live E2E.

## 4. Chrome unit tests

Node `node --test`.

- normalization
- protocol parse
- protocol failure codes
- duplicate group selection
- remembered tab selection
- Unicode group query

Pure functions を lib へ抽出して Chrome API mock を最小化。

## 5. Chrome smoke test

Local Chrome developer mode:

1. extension load
2. popup shows ID/version
3. group list
4. direct Dispatcher `研究`
5. direct Dispatcher `7脱`
6. not-found notification
7. startup collapse toggle
8. no Dispatcher tab residue

## 6. Live Windows E2E

これは本人の machine で行う。

### E2E-A: Temporary safe shortcut

空き key F を一時利用可能なら:
- temporary managed shortcut
- action writes marker file or opens known local target
- SendKeys/manual Ctrl+Alt+F
- result verify
- cleanup

既存設定と conflict があれば別空き key。

### E2E-B: Chrome Research
- non-Chrome foreground
- Ctrl+Alt+R
- Chrome foreground
- 研究 expanded
- remembered tab
- dispatcher gone

### E2E-C: Chrome 7脱
same for X.

### E2E-D: Representative types
- Website
- Application
- BAT
- Chrome group

1件ずつ HotKey 起動を確認。

### E2E-E: No residency
Shortcut Manager close 後:
- process absent
- shortcuts still work

## 7. Acceptance evidence

`docs/verification/VERIFICATION.md` を Codex が更新。

必須:
- environment
- commands
- test counts
- package paths
- manual/live verification results
- known limitations
- any blocked external publication step

## 8. Release gate

以下すべて green:
- dotnet format check or compiler warnings policy
- dotnet build Release
- dotnet test
- node tests
- package script
- version consistency
- artifact SHA256
- local smoke verification recorded

CWS/Microsoft Store publication credentials は v0.1.0 build gate ではない。
