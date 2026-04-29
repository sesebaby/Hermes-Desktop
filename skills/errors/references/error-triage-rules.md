# Error Triage Rules

Use this file when deciding whether to search, create, or update the error store.

## Progressive Reading

Do not read every error entry.

1. Search by task keywords, module names, file paths, bug class, and review language.
2. Reduce the candidate set to the smallest relevant subset, usually 3-7 entries.
3. Read only:
   - `root_cause`
   - `bad_fix_paths`
   - `corrective_constraints`
   - `verification_evidence`
4. Deep-read the full entry only when the current task is strongly similar.

## Recording Thresholds

### MUST record

- Real implementation failure caused rework, rollback, or repeated edits.
- Review found a bug, regression risk, contract drift, missing test, or wrong fix direction.
- The same class of mistake happened again.
- The root cause is procedural, not a one-off typo.

### SHOULD record

- Resolution took multiple wrong attempts.
- The lesson is likely reusable across projects or modules.
- The user explicitly points out a recurring failure pattern.

### Usually do not record

- Single typo with no reusable lesson.
- Transient environment flake with no process gap.
- A case fully covered by an existing entry with nothing new to add.

## Update Vs Create

Default to `update`.

Update an existing entry when:

- the root cause matches
- the preventive constraint matches
- only symptoms, bad paths, or verification evidence are new

Create a new entry when:

- the root cause is materially different
- the corrective constraint is different
- the old entry would become ambiguous if merged

## Mandatory Decision Output

After any development failure or review finding, emit:

```md
Error Recording Decision
- Record to errors: yes/no
- Reason:
- Action: create/update/skip
- Error file:
```
