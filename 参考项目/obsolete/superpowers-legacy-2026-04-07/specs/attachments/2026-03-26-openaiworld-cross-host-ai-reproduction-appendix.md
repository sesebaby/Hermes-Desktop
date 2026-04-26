# OpenAIWorld Cross-Host AI Reproduction Design

## 1. Purpose

This document defines how this work will produce a new, code-first reproduction manual for the `GGBH_OpenAIWorld` NPC/world AI system.

The target is not a reverse-engineering report.

The target is an implementation-first manual that another AI worker can follow to reproduce the system's core behavior inside other host games such as Stardew Valley, RimWorld, or 太吾绘卷.

## 2. Truth-Source Order

All outputs from this work must follow this truth-source order:

1. Decompiled code and recovered assets
2. Decoded prompts and runtime artifacts
3. Existing analysis documents
4. Clearly marked inference

If an existing analysis file conflicts with code, code wins.

If code is incomplete because of obfuscation, the conclusion must be marked as inference or unresolved.

## 3. Deliverable Shape

The final deliverable will use `1 master manual + appendices`.

### 3.1 Master Manual

The master manual must be written for execution, not for admiration.

It must explain:

- what the system does
- what modules exist
- what data flows through them
- what host-facing contracts are required
- which requirements belong to the portable cross-host core versus repo-specific governance overlays
- what minimum implementation order is safest
- what can be reproduced exactly
- what must be adapted per host game

The master manual must make the `MVP + complete framework` path primary.

It may include wider coverage of confirmed expansion mechanisms, but those cannot bury the main implementation path.

### 3.2 Appendices

The appendices must separate evidence and uncertainty from the main implementation path.

Required appendices:

- code evidence map
- source-document validation table
- host-porting appendix
- unresolved and inferred items appendix
- prompt-asset preservation and comparison appendix

## 4. Scope

The manual must cover the full AI system itself, with emphasis on the following primary chains:

1. private dialogue
2. group chat
3. memory
4. active world / world event generation

The manual must also include the following as first-class framework concerns:

- information propagation
- long-term memory compression
- relation influence
- prompt layering
- structured output and deterministic application
- host adapter boundaries
- persistence and replay evidence

The manual may include additional confirmed mechanisms where code evidence exists, but the above list defines the mainline.

## 5. Required Evidence Discipline

Every major claim cluster in the master manual must be auditable by one of these two compliant patterns:

1. inline claim-level evidence labeling
2. section-level status/policy labeling plus an explicit pointer to Section 3 and the appendices for the detailed proof trail

If pattern 1 is used, each major claim must carry:

- code anchor
- artifact or prompt anchor when relevant
- confidence label
- status label:
  - `confirmed by code`
  - `supported by artifact`
  - `inference`
  - `unresolved`

If pattern 2 is used, the section must clearly declare whether it is `confirmed`, `rebuild policy`, `repo overlay`, `inference`, or `unresolved`, and it must state where the detailed anchors live.

The master manual may summarize evidence inline, but the full proof trail belongs in appendices.

## 6. Intended Reader

The intended reader is an AI engineering worker with no prior context.

The document must therefore be explicit about:

- module boundaries
- required inputs and outputs
- execution order
- host responsibilities
- failure modes
- what not to copy literally from the original mod

## 7. Reproduction Goal

The reproduction goal is not "clone this exact game mod."

The reproduction goal is:

- preserve the reusable AI mechanics and execution pattern
- preserve the player-visible semantics
- avoid carrying over host-specific shell, vocabulary, UI, or world fiction unless needed

`OpenAIWorld` is therefore treated as:

- mechanics anchor
- semantic anchor
- not a host-shell template

## 8. Review Standard

The final manual will be reviewed against one question:

Can an AI worker use this document to rebuild the system in another host game without inventing missing architecture?

Reviewers must prioritize:

- factual accuracy
- missing system boundaries
- hidden assumptions
- reproduction ambiguity
- unsafe inference presented as fact
- gaps in host adaptation guidance

Style-only feedback is secondary.

## 9. Review Process

The final manual must go through at least `3` full review-and-fix rounds.

Constraints:

- round 1 uses at least `6` fresh `GPT-5.4 high` agents
- each later round must use fresh agents, not reused history-heavy reviewers
- prior review agents must be closed before the next round begins
- the loop ends only when no `high` or above issues remain

## 10. Completion Standard

This work is complete only when all of the following are true:

- the master manual exists
- required appendices exist
- the manual is code-first and does not depend on prior analysis accuracy
- at least `3` review/fix rounds are complete
- no `high` or above review issues remain in the last round
