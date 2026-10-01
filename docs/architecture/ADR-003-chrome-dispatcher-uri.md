# ADR-003: `chrome-extension://` Dispatcher URI

Status: Accepted / Windows実機検証済み

## Decision
Native Messaging / localhost / Named Pipe を使わず、
Chrome executable に extension dispatcher URL を渡す。

正式 protocol:
`?v=1&action=focus-group&group=<encoded>`

## Consequence
one-way。
Extension install state の native callback は v1 では持たない。
