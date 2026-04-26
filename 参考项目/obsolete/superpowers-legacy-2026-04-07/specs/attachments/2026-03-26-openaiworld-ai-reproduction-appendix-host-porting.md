# Appendix C: Host-Porting Guide

> Current-architecture interpretation note:
>
> This appendix still defines useful porting constraints.
> But if a passage sounds like the rebuilt orchestration owner must stay local, treat that as source-era context rather than the current repo-wide deployment rule.

## 1. Porting Principle

Port the mechanics and player-visible semantics.

Do not port the xianxia shell.

The target player experience should become:

- NPCs remember
- relations change how they respond
- group chat has turn structure
- information can travel between actors
- the world can create new event objects over time

How that looks in each host game should change.

## 2. Host Responsibilities

Every host adapter must implement seven responsibilities.

### 2.1 Identity

- canonical stable actor ids
- canonical stable location ids
- canonical stable event ids
- mapping from canonical ids to ephemeral host handles when the host does not expose stable native ids

### 2.2 Time

- world progression signal
- one fixed long-memory bucket unit such as month, season, or another documented summary bucket chosen once per port
- legality gates that decide whether AI communication or world events may surface now

### 2.3 Social graph

- actor-to-actor relation view
- optional faction or group membership

### 2.4 Scene context

- where an actor is
- who is nearby or logically reachable
- current environmental metadata if the host has it

### 2.5 Deterministic action apply

- a registry of allowed portable action types
- validation of action args against schemas
- translation of allowed action types into host-native legal intents
- routing those intents through the host's own legal systems instead of raw field mutation

### 2.6 Messaging persistence

- direct message persistence
- local group message persistence
- remote routed communication persistence through one or more host-native multi-speaker room/thread surfaces
- mirrored actor-local replay history
- sidecar or audit persistence for replay/debugging

### 2.7 Event surfacing

- a visible surface for world events
- a durable event object with both narrative lifecycle state and commit/apply state
- host-native event surfacing such as incidents, quests, map events, site objects, or encounter objects

## 3. What Must Stay Invariant Across Hosts

- channels remain separate
- model output remains structured
- deterministic validation remains between model and host
- raw history and compressed memory remain separate
- propagation creates real receiver-visible state
- active world produces durable event state
- remote routed communication remains distinct from one-off propagation carriers
- world events preserve both narrative lifecycle and host commit/apply state

## 4. What Must Change Per Host

- fiction
- item taxonomy
- state taxonomy
- conversation carriers and surfaces
- world-event surfaces
- allowed action whitelist
- pacing and tick frequency

## 5. Stardew Valley Mapping

### 5.1 Good fit

- clear day progression
- town/house/farm locations are easy scene anchors
- social relationships are already central
- mail and gossip fit propagation
- festivals and cutscene/event objects fit group chat and active-world surfacing

### 5.2 Recommended mappings

- private dialogue:
  - direct NPC conversation
- group chat:
  - festival scenes
  - saloon scenes
  - cutscene-style group dialogue
- remote routed communication:
  - a persisted town thread, party-line scene, or shared remote discussion board with per-speaker turns
- propagation:
  - letters
  - bulletin-board rumors
  - persisted gossip delivery records that later become dialogue context
- active world:
  - town-incident objects with surfaced/triggered/resolved state
  - farm-visit event objects
  - quest-like world-event objects

### 5.3 Watchouts

- many original OpenAIWorld actions have no one-to-one Stardew equivalent
- for an early prototype you may start with a smaller action whitelist, but a faithful port must restore the canonical minimum action vocabulary from the master manual
- memory buckets may fit season or week better than month depending on pacing, but choose one bucket unit per save and keep it fixed

## 6. RimWorld Mapping

### 6.1 Good fit

- explicit world/colony state
- strong event system
- clear faction and relation semantics
- host already supports durable world incidents

### 6.2 Recommended mappings

- private dialogue:
  - pawn-to-pawn interaction records
  - social logs
- group chat:
  - colony meetings
  - recreation-room exchanges
  - caravan camp talk
- remote routed communication:
  - faction conference threads
  - radio-net room conversations
- propagation:
  - radio reports
  - rumor spread between pawns or settlements
- active world:
  - incident queue integration with durable event ids/state
  - site discovery event objects
  - passing-trader stories becoming queued incidents

### 6.3 Watchouts

- deterministic application must respect RimWorld's simulation, reservation, and job system
- do not let AI directly bypass incident, job, quest, or letter validation
- world events should map to incidents, quests, or site objects, not only dialogue text
- letters may notify the player about an event, but should not replace the durable event object itself

## 7. 太吾绘卷 Mapping

### 7.1 Good fit

- dense relationship graph
- sect/stance/social structure
- long-lived characters and timeline
- martial-world rumors and encounters map naturally to propagation and active world

### 7.2 Recommended mappings

- private dialogue:
  - direct interaction scenes
- group chat:
  - sect gatherings
  - village meetings
  - roadside encounters
- remote routed communication:
  - sect-network discussion threads
  - message-board style remote conference surfaces if the mod layer supports them
- propagation:
  - rumor system
  - sect gossip
  - letters or notifications if supported by the modding layer
- active world:
  - jianghu incident objects with surfaced/triggered/resolved state
  - sect-conflict event objects
  - discovery and traveling-NPC event-chain objects

### 7.3 Watchouts

- relation semantics are richer than a single intimacy number
- preserve actor-relative relation views if the host supports richer social dimensions
- do not import xianxia-specific action names directly; keep the portable action vocabulary and translate it into Taigu-specific deterministic intents only at the host adapter boundary

## 8. First-Port Strategy

Use the same phased order for every host:

1. contracts, id mapping, prompt resolution, and action registry
2. private dialogue
3. long memory
4. group chat
5. propagation
6. active world

Why:

- contract work prevents each later channel from inventing its own schema
- private dialogue proves the request-parse-apply loop
- long memory proves persistence and summarization
- group chat proves multi-actor orchestration
- propagation proves cross-actor state transfer
- active world proves autonomous event creation

## 8.1 Required Event-State Mapping

Every host port must explicitly map both of these:

- narrative lifecycle: `proposed -> surfaced -> triggered/resolved/expired -> deleted`
- commit/apply state: `pending -> applied/delayed/skipped/rolled_back/failed`

The host profile must say:

- which host object stores the durable event
- which player-visible surface marks `surfaced`
- which host interaction marks `triggered`
- which host writeback or audit path records `eventState`
- which rollback path can emit `rolled_back`

## 9. Host Adapter Checklist

Before starting a port, answer each question explicitly.

- What is an actor id in this game?
- What is a location id in this game?
- If those ids are unstable, where will the AI layer keep its canonical id mapping?
- What counts as a time bucket?
- Which host legality gates must AI pass before surfacing dialogue, deliveries, or events?
- Where can direct dialogue be shown?
- Where can group dialogue be shown?
- Which host-native room/thread surfaces support remote group communication, and which one-off carriers support propagation deliveries?
- What objects represent durable world events?
- How do those world-event objects map narrative lifecycle versus commit/apply state?
- Which portable action types can be translated into host-native legal intents without unsafe side effects?
- Which content factories are allowed to create actors or items for world events?
- What should be logged for replay and debugging?

If any answer is missing, the port design is not ready.
