# Appendix D: Unresolved And Inference-Bound Items

> Current-architecture interpretation note:
>
> This appendix records what remains unresolved in the recovered original system.
> It should not be read as a requirement that the rebuilt orchestration must remain local.
> Use it for claim hygiene; use the active framework docs for current deployment ownership.

## 1. Purpose

This appendix exists to stop weakly supported conclusions from leaking into the main manual.

If an item is here, it may be a useful lead, but it is not a stable fact yet.

`partially resolved` here means the rebuild contract now defines an explicit portable strategy, not that the original obfuscated implementation has become fully recovered.

## 2. Exact Prompt Load And Override Order

- Current status: `partially resolved`
- What is known:
  - prompts are resolved through an obfuscated manager
  - it can load writable overrides or bundled prompt assets
  - it performs placeholder substitution
  - the rebuild manual now fixes an explicit precedence contract for the reproduced system
- What is unresolved:
  - exact precedence and all toggles/guards in every original runtime path

This unresolved historical point must not be used to reopen the rebuild contract.

## 3. Exact Action Dispatcher Ownership

- Current status: `partially resolved`
- What is known:
  - parsed action lists are produced after JSON parsing
  - downstream callbacks receive both content and typed action lists
  - host writeback is not equivalent to raw model text
  - `D.I.A(WorldUnitBase, string, List<u.W>, ...)` is one concrete dispatcher path
  - the rebuild manual now defines a portable action registry boundary, audit schema, and channel allowlists
- What is unresolved:
  - exact original complete command table and every caller path for every channel

This unresolved historical point does not mean ports may invent arbitrary JSON envelopes.

Use the canonical minimum vocabulary and schemas from the master manual unless stronger code anchors are found.

## 4. Exact Propagation Router

- Current status: `partially resolved`
- What is known:
  - propagation prompt exists
  - contact groups exist
  - direct and group message stores exist
  - one readable direct router is now code-anchored
  - that direct router resolves one target actor and chooses:
    - local private dialogue when co-located
    - remote direct communication when not co-located
  - the rebuild manual treats propagation as persisted message traffic, not only world-event text
  - the rebuild manual now defines explicit routing strategy hooks plus a documented reference routing profile
- What is unresolved:
  - whether the original implementation had additional readable propagation branch paths beyond the now-anchored direct one-to-one local/remote split
  - exact original conditions, if any, for routing propagation into contact-group or other indirect carriers

This unresolved historical point does not permit ports to skip deterministic routing, lineage logging, or documented anti-explosion policy.

## 5. Full Group-Chat Turn Scheduler

- Current status: `partially resolved`
- What is known:
  - group-chat persistence exists
  - speaking-order prompt exists
  - a separate planning request returning an `npcs` array is now code-anchored
  - per-speaker generation and per-turn effect application are now code-anchored
  - the rebuild manual now fixes a sequential transaction model for faithful reproduction
- What is unresolved:
  - exact original scoring, heuristics, and every caller path that consume the order prompt and emit the final per-speaker order

## 6. Full Active-World Class Identity

- Current status: `partially resolved`
- What is known:
  - `O.cs` contains the active-world scheduler, parse, and event-materialization path
  - `E.cs` marks corresponding events triggered on interaction
- What is unresolved:
  - exact unobfuscated semantic names for every helper object involved

## 7. Exact Long-Memory Runtime Reinjection

- Current status: `unresolved`
- What is known:
  - `ExperienceData` exists as month-bucketed summary storage
  - compression, export/import, and UI display are code-anchored
  - the rebuild manual treats reinjection as an optional reconstruction strategy only
- What is unresolved:
  - exact original runtime paths, if any, that read `ExperienceData` back into generation requests

## 8. Full Prompt Literal Mapping

- Current status: `unresolved`
- What is known:
  - the code clearly requests prompt text through keys and a prompt manager
  - decoded prompt assets strongly match the observed channel architecture
- What is unresolved:
  - one-to-one mapping for every obfuscated key to every literal prompt filename at every callsite

## 9. Safe Engineering Response To These Unknowns

When rebuilding the system:

- preserve the confirmed architecture
- expose unknowns as configurable strategy hooks
- keep trace and replay logs around every strategy hook
- never bake a guessed original behavior into a non-configurable core

## 10. If Future Review Finds Stronger Anchors

Promote an item from this appendix into the master manual only if:

1. a direct code anchor is found
2. or multiple independent artifact/code anchors reduce ambiguity enough to make the claim stable

Until then, the item stays here.
