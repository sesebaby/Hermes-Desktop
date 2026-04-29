# Error Entry Template

Use this template when creating a new error record.

Prefer repository-local templates when they exist. For AllGameInAI, prefer `D:\GitHubPro\AllGameInAI\openspec\errors\_template.md`.

```md
# E-YYYY-NNNN-short-slug

- id: E-YYYY-NNNN
- title:
- status: active
- updated_at: YYYY-MM-DD
- keywords: [keyword1, keyword2, keyword3]
- trigger_scope: [module, workflow, bugfix, review, refactor]

## Symptoms

- What broke?
- What did the user or reviewer observe?

## Root Cause

- What was actually wrong?
- Why did the earlier reasoning fail?

## Bad Fix Paths

- Which incorrect repair attempts happened or were likely?
- Which path must not be repeated?

## Corrective Constraints

- Constraint 1
- Constraint 2
- Constraint 3

## Verification Evidence

- Which test, repro, or manual check proves the fix?
- Which signal proves the old mistake did not recur?

## Related Files

- path/to/file
- path/to/test

## Notes

- Optional extra detail that may help future triage.
```
