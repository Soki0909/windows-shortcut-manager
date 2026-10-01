# 12. Security / Privacy / Failure Policy

## 1. Local-only by default

No telemetry.
No analytics.
No external backend.
No cloud sync.
No browser page content collection.

## 2. Sensitive data

Do not log:
- passwords
- tokens
- cookies
- page content
- private keys

Tab URL:
- Extension local storage only for last-tab recovery
- not sent externally

## 3. Least privilege Chrome

No host permissions.
No content scripts.
No remote code.
No externally_connectable.

## 4. Filesystem boundary

Shortcut Manager destructive writes only:
- config/state/log/backups own LocalAppData directory
- managed Start Menu folder after ownership marker validation

Import source files:
read-only unless future explicit migration/delete feature.

## 5. No privilege escalation

Normal operations:
no admin.

ProgramData Start Menu:
scan read-only.

## 6. Failure policy

### Config invalid
- do not Apply
- do not delete managed links
- preserve raw broken config
- report path

### Target missing
- shortcut status Error
- block Apply for enabled invalid entry
- disabled invalid entry may remain stored but is not generated

### One staged `.lnk` fails
- abort stage
- current installation unchanged

### Commit fails
- rollback
- show failure
- retain backup

### Chrome extension missing/wrong ID
- normal Website/App/BAT unaffected
- Chrome group Test fails at launch/Chrome page
- validation can warn ID format but cannot prove install state without adding native bridge

## 7. External publication blockers

If Codex reaches steps requiring:
- Chrome Web Store account
- Store listing approval
- Microsoft Store account
- code signing certificate
- repository visibility decision

it must not fake completion.

It completes all credential-free artifacts and records exact remaining action in verification.
