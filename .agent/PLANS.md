# ExecPlan Rules

Use an ExecPlan for the v0.1.0 implementation and any later change that spans multiple components or requires multiple validation loops.

The plan must stay usable by a developer who has only the repository.

Keep these sections current:

## Purpose / Done condition
What must be true at completion and how it is verified.

## Progress
Checkbox milestones with timestamps or concise status.

## Discoveries
Facts learned from the actual repository, Windows, Chrome, tests, or packaging.
Record surprises that change implementation details.

## Decision Log
Only implementation-level choices not already fixed by requirements/design/ADR.
Do not reopen accepted architecture without a demonstrated blocker.

## Milestones
Each milestone states:
- code/artifacts to produce
- exact validation command(s)
- pass condition

## Verification Evidence
Commands executed and results.
Do not replace evidence with statements like "should work".

## Blockers
External credential/permission/tool blockers and the exact input required to continue.

Plan rule:
If validation fails, repair before advancing to the next milestone.
