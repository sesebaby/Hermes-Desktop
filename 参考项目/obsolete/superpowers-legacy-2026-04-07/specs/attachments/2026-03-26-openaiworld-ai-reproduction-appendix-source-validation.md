# Appendix B: Existing Analysis Document Validation

> Current-architecture interpretation note:
>
> This appendix defines evidence discipline for the recovered source set.
> It remains authoritative for source-validation method, but not for deciding whether rebuilt orchestration lives locally or on a hosted service.

## Validation Rule

This appendix does not judge whether an old analysis document is useful.

It judges whether it is safe to use as a truth source for the new reproduction manual.

Statuses:

- `trusted reference`
- `use cautiously`
- `do not trust for mainline facts`

These are document-reuse statuses only.

They are not a replacement for the master manual's code-truth versus inference boundary.

## 1. README.md

- Status: `trusted reference`
- Why:
  - directory layout and recovered-assembly framing match the recovered tree
  - loader-versus-real-assembly distinction matches available artifacts and decompiled source structure
- Caveat:
  - recovery-process narration is historical context, not needed for the reproduction manual's mainline

## 2. ANALYSIS_INDEX.md

- Status: `trusted reference`
- Why:
  - mostly an index and reading guide
- Caveat:
  - it should not be treated as proof for subsystem behavior

## 3. NPC_AI_IMPLEMENTATION_BLUEPRINT.md

- Status: `use cautiously`
- Why:
  - its top-level decomposition is directionally aligned with recovered code and prompts
  - it correctly emphasizes prompt layer, memory layer, channel separation, and execution layer as major concerns
- Caveat:
  - many detailed claims in that file still require direct obfuscated-code anchors before reuse as fact
  - especially claims about exact relation-summary injection, exact action dispatcher ownership, and exact active-world chain ownership

## 4. PRIVATE_CHAT_DECISION_CHAIN.md

- Status: `use cautiously`
- Why:
  - the existence of a dedicated private-chat chain is strongly supported by code
  - the file's broad claim that generation and execution are separated matches recovered structure
- Caveat:
  - exact request composition details and relation-view injection details remain only partially anchored in current code review
  - long-memory reinjection into runtime generation is not currently established as recovered fact

## 5. GROUP_CHAT_DECISION_CHAIN.md

- Status: `use cautiously`
- Why:
  - dedicated group-message persistence is real
  - dedicated speaking-order prompt is real
  - per-turn persistence and replay are real
  - separate order-planning plus sequential per-speaker generation is now code-anchored
- Caveat:
  - exact original ranking heuristics and every call path behind speaking-order selection still remain partially obfuscated

## 6. LONG_TERM_MEMORY_ANALYSIS.md

- Status: `use cautiously`
- Why:
  - month-bucketed `ExperienceData` is confirmed
  - export/import and summary display are confirmed
  - memory compression prompt is confirmed
- Caveat:
  - any claim about exact automatic compression timing or future-prompt reinjection must stay downgraded until fully anchored in obfuscated code

## 7. INFORMATION_PROPAGATION_CHAIN.md

- Status: `use cautiously`
- Why:
  - `ConveyMessage` prompt asset exists
  - contact-group structures exist
  - action parsing infrastructure exists
- Caveat:
  - exact delivery-path chooser and propagation anti-explosion mechanisms are not yet fully anchored in code
  - the rebuild manual therefore exposes explicit routing strategy hooks and a documented reference profile instead of inheriting old analysis guesses as historical fact

## 8. ACTIVE_WORLD_AI_ANALYSIS.md

- Status: `use cautiously`
- Why:
  - world prompts exist
  - durable world-event structures exist
  - map-event lifecycle hooks exist
  - active-world scheduling and persistence are now code-anchored
- Caveat:
  - exact class-name identity and some object-materialization details still remain obfuscated
  - prompt-level payload fields such as `content`, `items`, `units`, `addUnits`, `location`, and `locationType` are anchored at the prompt layer, but the portable normalized field names used by the rebuild manual are still a reconstruction layer, not recovered internal member names

## 9. PROMPT_STORAGE_ANALYSIS.md

- Status: `use cautiously`
- Why:
  - decoded prompt assets and manifest exist
- Caveat:
  - exact original override order and every runtime load path still need stronger code anchors before being reused as a historical fact
  - the rebuild manual intentionally defines its own explicit precedence contract so ports do not invent one ad hoc

## 10. Files Primarily Useful As Secondary Reading

- `NPC_AI_SYSTEM_MASTER_ARCHITECTURE.md`
- `FIRST_OTHER_GAME_MOD_MVP_TEMPLATE.md`
- `GGBH_OpenAIWorld_跨游戏AI通用方案.md`
- `ACTIVE_WORLD_AI_PLAIN_EXPLAINED.md`

Recommended status for these:

- `use cautiously`

Reason:

They are likely valuable synthesis documents, but the new manual must not inherit claims from them unless the claims are re-anchored to code or prompt assets.

## 11. Main Rule For Reuse

A claim from any old analysis document may enter the new manual only if one of the following is true:

1. it is directly anchored to recovered code
2. it is directly anchored to recovered prompt assets or recovered serialized data structures
3. it is clearly labeled as inference

If none of those are true, the claim must stay out of the mainline manual.
