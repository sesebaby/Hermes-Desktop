# OpenAIWorld AI System Cross-Host Reproduction Manual

## 1. Document Contract

This manual is for rebuilding the `GGBH_OpenAIWorld` NPC/world AI system in another host game.

This is not a reverse-engineering diary.

This is an implementation manual.

Its job is to tell an AI worker:

- what the original system actually contains
- what abstractions are reusable across games
- what must be implemented first
- what host interfaces are required
- what parts are confirmed by code
- what parts are only inferred and must not be treated as fact

Truth-source order for this manual:

1. decompiled code
2. recovered assets and decoded prompts
3. existing analysis docs
4. inference clearly marked as inference

If this manual conflicts with old analysis notes, code wins.

## 1.1 Current Repo Target-Architecture Note

This manual documents the recovered original system and the faithful reproduction contract.

It does not, by itself, decide where orchestration must run in the current product architecture.

Under the current repo-level framework:

- preserve the recovered chain structure, data ordering, and prompt-asset lineage from this manual
- but allow the orchestration runtime to move from a host-local implementation to a hosted orchestration service
- keep deterministic execution and authoritative host writeback on the local host side
- treat long-memory truth, canonical recent-history truth, and orchestration truth as service-side concerns in the current product direction unless a newer approved contract says otherwise

In short:

- this manual is still the source for "how the chain works"
- the top-level framework docs decide "where that chain lives"

## 2. What The System Really Is

`OpenAIWorld` is not just "NPC chat with prompts."

The recoverable code and assets show a layered system:

1. host hooks patch the game lifecycle and world/unit/event entrypoints
2. channel-specific AI flows assemble structured context
3. decoded prompt files define world rules, channel rules, memory compression, behavior commands, and propagation rules
4. model output is expected as JSON, then parsed and repaired if needed
5. parsed actions are filtered and applied through deterministic game-side logic
6. messages, experiences, and world events are persisted back into game data structures
7. future turns reuse persisted records as history, while summary-memory reinjection remains a configurable rebuild choice rather than a fully proven recovered fact

That architecture, not the xianxia setting, is the thing worth porting.

## 3. Confirmed Capability Map

### 3.1 Confirmed By Code

- Loader object exists and delegates to a deeper runtime object rather than containing business logic itself.
  Evidence: `OpenAIWorld/Loader.cs`
- Harmony patches are installed across data, world, unit, map-event, UI, and asset-loading paths.
  Evidence: `OpenAIWorld/PatchClass.cs`
- The mod defines explicit message data types for private chat, group chat, and contact-group chat.
  Evidence: `OpenAIWorld.mod.data/MessageData.cs`, `PrivateMessageData.cs`, `GroupMessageData.cs`, `ContactGroupMessageData.cs`
- The mod defines an explicit long-memory summary object, `ExperienceData`, keyed by month.
  Evidence: `OpenAIWorld.mod/ExperienceData.cs`
- The mod defines explicit world-event persistence data, `WorldEventData`.
  Evidence: `OpenAIWorld.mod/WorldEventData.cs`
- The LLM request/response contract includes chat messages, JSON response-format hints, and tool-call shapes.
  Evidence: `OpenAIWorld.ailm/ChatCompletions.cs`
- The mod keeps a conversation/session abstraction separate from transport details.
  Evidence: `OpenAIWorld.ailm/LLMSession.cs`
- Prompt resolution is managed through a prompt manager that can load writable overrides or bundled assets and then perform placeholder substitution.
  Evidence: obfuscated prompt manager in `.../O.cs` and `.../s.cs`
- JSON repair logic is bundled locally, so malformed model JSON is treated as an expected runtime condition.
  Evidence: `jsonrepair/JsonRepair.cs`
- Private chat writes mirrored `PrivateMessageData` records for both sides and includes round day, time, point, weather, and message role.
  Evidence: obfuscated private-chat manager in `.../F.cs`
- Group chat history is rendered from persisted `GroupMessageData` records with sender, content, time, place, and weather.
  Evidence: obfuscated group-chat UI/controller in `.../F.cs`
- Long-memory summaries are exportable, importable, removable, and displayed as month-bucketed `ExperienceData`.
  Evidence: obfuscated memory UI/controller in `.../s.cs`
- World events are scheduled, persisted, surfaced, and marked triggered after player interaction.
  Evidence: obfuscated active-world logic in `.../O.cs` and event-open handler in `.../E.cs`

### 3.2 Confirmed By Assets Or Prompts

- There is a dedicated memory-compression prompt that turns chat/event history into compressed memory summaries.
  Evidence: `decoded_prompts/记忆压缩.md`
- The long-memory compression pass is also implemented in code, not just described in docs/prompts.
  Evidence: obfuscated memory compressor in `.../g.cs`
- There is a dedicated action schema prompt that requires an `actions` array with named commands and typed args.
  Evidence: `decoded_prompts/行为指令.md`
- There is a dedicated propagation prompt around `ConveyMessage`.
  Evidence: `decoded_prompts/信息裂变.md`
- There is a dedicated world-rules prompt and a separate world-event generation prompt.
  Evidence: `decoded_prompts/世界定义.md`, `decoded_prompts/世界推演.md`
- There is a dedicated prompt for selecting group-chat speaking order.
  Evidence: `decoded_prompts/群聊_发言顺序引导.md`

### 3.3 Inference, Not Yet Strong Enough For Mainline Fact

- Exact class ownership for every prompt-loading path inside obfuscated code
- Exact propagation channel selector logic for local talk versus remote messaging
- Exact prompt override precedence from bundled assets versus disk overrides

These can guide investigation but cannot be treated as settled architecture.

## 4. Reusable Architecture To Copy

Rebuild the system as six hard layers.

Additional hard rule for this repository:

- `Phase A: source-faithful reproduction first`
  - first prove the recovered six-layer chain runs with source-style storage, repair/normalize, projector/executor, replay, and memory feedback
- `Phase B: repo overlay second`
  - only after Phase A passes do we add repo-facing bridges, hosted truth redistribution, or shared platform abstractions

Labeling rule for Sections 4-12:

- `[CONFIRMED]` means directly anchored to recovered code, prompt assets, or serialized data
- `[REBUILD POLICY]` means a portability contract added so another host can reproduce the behavior without inventing ad hoc rules
- `[REPO OVERLAY]` means additional requirements that only apply when the rebuilt system is integrated into this repository's runtime/governance stack

Evidence-location rule:

- Section 3 states the recovered facts directly
- Section 5 mixes recovered storage core with explicitly labeled rebuild overlays
- Appendix A and Appendix B carry the detailed code/prompt anchors
- Appendix D lists unresolved items that must not be promoted back into mainline fact without new anchors

### 4.1 Layer A: Host Hook Layer `[REBUILD POLICY]`

Responsibilities:

- subscribe to game lifecycle
- subscribe to world/day progression
- subscribe to NPC/unit creation and action creation
- subscribe to map-event creation/open/delete
- surface AI UI or debugging panels if the host supports them

What to copy:

- patch or event-hook entrypoints
- isolated AI subsystem startup and shutdown
- no direct game logic inside the loader shell

What not to copy literally:

- exact Harmony patch names
- exact UI prefabs
- xianxia game object names

### 4.2 Layer B: Canonical Snapshot Layer `[REBUILD POLICY]`

Before calling a model, always convert host state into portable AI snapshots.

At minimum define:

- `ActorSnapshot`
- `RelationSnapshot`
- `SceneSnapshot`
- `WorldClockSnapshot`
- `ChannelHistorySnapshot`
- `LongMemorySnapshot`
- `HostActionContext`

Reason:

The original system stores host-rich data, but the prompts and request builders imply that the model sees reduced semantic summaries, not raw object graphs.

#### 4.2.1 Snapshot Ownership Boundary

Use this ownership split:

- host adapter owns raw game-object reads, legality checks, and host-handle lookup
- canonical snapshot layer owns durable AI ids, normalization, redaction, and serialization
- channel orchestrators may read snapshots but must not reach back into raw host objects

If the host does not expose naturally stable ids for actors, locations, or events, the AI layer must mint durable ids and keep a mapping table back to ephemeral host handles.

#### 4.2.2 Required Minimum Snapshot Fields

`ActorSnapshot`

- `actorId`
- internal `hostHandleRef` or equivalent lookup token
- `displayName`
- `kind` such as `npc`, `player`, `faction-agent`, `animal`
- `locationId`
- `stateTags[]`
- `roleTags[]`
- `inventorySummary[]`
- `statusSummary[]`
- `scheduleOrDutySummary`
- `memoryAnchorKeys[]`
- `canSpeak`, `canTravel`, `canTrade` capability flags

`RelationSnapshot`

- `ownerActorId`
- `otherActorId`
- `stanceTags[]`
- `affinityScore` or host-equivalent scalar
- `trustScore` if available
- `authorityDelta` or social-rank hint if available
- `lastKnownInteractionSummary`
- `relationshipFacts[]`

`SceneSnapshot`

- `sceneId`
- `sceneKind`
- `displayName`
- `visibleActorIds[]`
- `reachableActorIds[]`
- `environmentTags[]`
- `weatherSummary`
- `safetyOrThreatTags[]`
- `hostLegalityTags[]`

`WorldClockSnapshot`

- `tickId` or monotonic progression marker
- `dayId`
- `seasonOrMonthId`
- `timeOfDay`
- `calendarLabels`
- `worldStateTags[]`

`ChannelHistorySnapshot`

- `channelType`
- `channelId`
- `participantActorIds[]`
- `recentTurns[]` with `turnId`, `speakerActorId`, `content`, `timestamp`, `environmentSummary`, `acceptedActions[]`
- `historyCutoffPolicy`

`LongMemorySnapshot`

- `ownerActorId`
- `timeBucket`
- `bucketLabel`
- `summaryText`
- `sourceTurnCount`
- `sourceLogCount`
- `summaryVersion`

`HostActionContext`

- `channelType`
- `initiatorActorId`
- `speakerActorId`
- `targetActorIds[]`
- `sceneId`
- `clock`
- `reachableActorIds[]`
- `allowedActionTypes[]`
- `hostLegalityTags[]`
- `traceId`

#### 4.2.3 Required Normalization Rules

- ids must be strings and stable across save/load
- all actor references must use canonical `actorId`, never raw pointers inside prompts
- all location and event references must use canonical ids even if host handles are nullable
- missing host data must become explicit `unknown` or empty collections, never omitted ad hoc
- relation snapshots are actor-relative views, not one global merged relation object
- recent history records must contain accepted deterministic outcomes, not raw model JSON only
- long-memory summaries must stay separate from short-term replay turns
- internal lookup refs such as `hostHandleRef` may exist inside adapter-private snapshot objects, but prompt serialization must drop them

### 4.3 Layer C: Channel Orchestration Layer `[REBUILD POLICY]`

Keep channels separate.

Do not build one generic `speak()` call and try to branch inside prompt text.

The system needs separate orchestrators or execution paths for:

- private dialogue
- local group chat
- remote routed communication
- information propagation protocol handling
- active world event generation

Each channel or protocol execution path should define:

- trigger condition
- participants
- context pack
- prompt bundle
- expected JSON schema
- deterministic post-processing path
- persistence writeback path

The recovered source uses a distinct `ContactGroup`/`ContactGroupMessageData` channel for one remote path.

For a cross-host rebuild, treat that as one source-specific implementation of the broader `remote routed communication` channel.

Remote routed communication must stay a dedicated multi-speaker remote room surface, not collapse into generic rumor delivery.

Ports may implement this channel through a host-native multi-speaker remote surface such as a party-link room, comms conference, sect network, social thread, or another persisted remote discussion room.

One-off letters, rumor boards, notifications, and other single-delivery carriers belong under the propagation protocol, not under this channel.

### 4.4 Layer D: Prompt Bundle Layer `[REBUILD POLICY]`

Treat prompts as structured assets, not hardcoded strings.

Do not discard the recovered decoded prompt corpus after normalizing it.

Preserve the original files unchanged as the comparison baseline described in Appendix E.

The recovered prompt set shows at least five reusable bundle classes:

1. world rules
2. channel rules
3. action protocol
4. propagation protocol
5. memory compression protocol

Recovered code shows prompt text is not normally hardcoded at the call site. It is resolved through a manager that can export bundled prompts into a writable game-data folder, load local overrides behind flags, and apply runtime variable substitution.

For cross-host reproduction, split prompt assets into:

- global invariant prompts
- host semantic overlays
- game fiction overlays
- optional writable local overrides

Example:

- keep "actions must be JSON" invariant
- replace "cultivation realm" vocabulary with the host game's own status or power language

#### 4.4.1 Rebuild Prompt Resolution Contract `[REBUILD POLICY]`

The exact original override precedence is still partially obscured.

Do not leave this ambiguous in the rebuilt system.

Use this explicit resolution order:

1. load global invariant bundle
2. merge host semantic overlay
3. merge game fiction overlay
4. if `EnablePromptOverrides=true`, apply writable local override
5. perform runtime placeholder substitution on the final resolved text
6. append provider-specific response-format hints outside the authored prompt body

This is a reconstruction contract for reproducibility, not a claim that every original code path used this exact order.

#### 4.4.2 Required Prompt Bundle Keys

Every port must publish a prompt manifest with these canonical keys:

- `world.rules`
- `channel.private_dialogue`
- `channel.group_chat`
- `channel.remote_routed_communication`
- `protocol.actions`
- `protocol.propagation`
- `protocol.memory_compression`
- `world.event_generation`
- `group.speaker_selection`
- `group.remote_speaker_selection`

Optional keys may be added, but these keys must exist even if some are thin wrappers around shared text.

Optional extension keys may also be normalized when the host wants the extra feature surface.

Current known optional extension:

- `channel.proactive_dialogue`

#### 4.4.3 Required Override Semantics `[REBUILD POLICY]`

Use whole-bundle replacement, not line-by-line merging.

For each prompt key:

- the invariant bundle always provides the base body
- host overlay may replace the full body for that key
- fiction overlay may replace the full body for that key
- writable local override may replace the full body for that key only when `EnablePromptOverrides=true`

If a higher layer does not define the key, the lower resolved body survives unchanged.

Persist a prompt-resolution manifest per request.

Portable core fields:

- `traceId`
- `channelType`
- `promptKeys[]`
- `winningLayerByKey`
- `contentHashByKey`
- `overrideEnabled`

Repo-governed overlay fields:

- `requestId`
- `launchSessionId`
- `skuId`
- `gameId`
- `billingSource`
- `channelType`
- `capability`
- `narrativeTurnId`
- `degradedMode`
- `traceGroupId`
- `claimStateRef`
- `claimStateAtEvent`
- `degradationWindowId`
- `degradationStartedAt`
- `evidenceReviewRef`
- conditional `escalationDeadlineAt`
- conditional `escalatedAt`
- conditional `waiverId`
- conditional `waiverLineageId`
- conditional `recoveryEvidenceRef`

The portable core portion of that manifest is mandatory for any faithful cross-host reproduction.

The repo-governed overlay portion is mandatory only when the rebuilt system is integrated into this repository's runtime/governance stack.

#### 4.4.4 Original Prompt Corpus Preservation Rule

The rebuilt system must keep:

- one read-only archive of the original decoded prompt files
- one normalized cross-host prompt layer
- zero or more host/fiction/local overlays

Never keep only the rewritten prompts.

Without the original comparison corpus, future review cannot distinguish:

- faithful invariant preservation
- host-semantic rewriting
- accidental behavioral drift

### 4.5 Layer E: Structured Output And Deterministic Application Layer `[REBUILD POLICY]`

This is the most important layer to preserve.

Model responsibilities:

- generate text
- propose actions
- propose propagation
- propose world-event structure

Deterministic layer responsibilities:

- repair malformed JSON
- parse JSON into typed contracts
- reject unknown commands
- validate arguments
- validate target existence
- validate scene or distance preconditions
- apply only whitelisted host actions
- persist both accepted and rejected results for debugging

Never let the model directly mutate the host.

Also never let the deterministic layer bypass host-native legal systems.

The deterministic layer should emit validated host intents, then route them through the target game's own legal application path such as jobs, incidents, interactions, quest/mail systems, schedule edits, or battle/social subsystems.

### 4.6 Layer F: Persistence, Replay, And Memory Layer `[REBUILD POLICY]`

Persist three different things separately:

1. raw channel history
2. compressed long memory
3. world-event history

Do not collapse them into one log.

Why:

- raw history is for recent-context replay
- compressed memory is for economical long-horizon reuse
- world events are global-state objects, not merely conversation turns

#### 4.6.1 Required Replay Envelope `[REBUILD POLICY]`

Every generated turn or world event proposal must produce one canonical replay envelope before any mirrored projections are written.

Portable core fields:

- `traceId`
- `canonicalRecordId`
- `channelType`
- `requestSnapshotRef`
- `resolvedPromptManifest`
- `rawModelResponse`
- `normalizedResult`
- `parseDiagnostics`
- `actionAuditRefs[]`
- `visibleRecordRefs[]`
- `mirrorProjectionRefs[]`
- `createdAt`

Repo-governed overlay fields:

- `requestId`
- `launchSessionId`
- `skuId`
- `gameId`
- `billingSource`
- `capability`
- `narrativeTurnId`
- `executionResult`
- `degradedMode`
- `traceGroupId`
- `claimStateRef`
- `claimStateAtEvent`
- `degradationWindowId`
- `degradationStartedAt`
- `evidenceReviewRef`

Lifecycle-conditioned fields:

- `escalationDeadlineAt` once an incident enters a degraded window
- `escalatedAt` once an incident crosses the escalation threshold
- `waiverId` only when waiver approval exists
- `waiverLineageId` only when the incident has entered a waiver review or renewal chain
- `recoveredAt` only when the incident is closed
- `recoveryEvidenceRef` only when the incident is closed

Mirrored actor-local histories may project this envelope into different inboxes or views, but they must all point back to the same canonical replay envelope.

If this system is implemented inside this repository, the overlay fields above become mandatory because they must deterministically join the launcher status log, runtime state log, and deterministic host writeback audit log.

Trace correlation rules for repo-governed implementations:

- pre-waiver degraded traces must at minimum join on `skuId + gameId + capability + billingSource + traceGroupId + claimStateRef`
- once a waiver is approved, degraded runtime evidence must join on `skuId + gameId + capability + billingSource + traceGroupId + waiverId + claimStateRef`
- `waiverId` must point to one concrete approved waiver instance, not a generic business key
- renewal continuity must preserve `traceGroupId + waiverLineageId + degradationStartedAt`

## 5. Core Data Contracts

This section defines the minimum portable contracts implied by recovered code.

### 5.1 Message Base Contract `[CONFIRMED CORE + REBUILD POLICY]`

Portable base fields:

- `index`
- `canonicalRecordId`
- `traceId`
- `surfaceId`
- `roundTotalDay`
- `dateTime`
- `message.role`
- `message.content`

Code anchor:

- `OpenAIWorld.mod.data/MessageData.cs`

Rebuild rule:

`index` is the source-shaped visible ordering field.

Do not rely on it as the only durable key.

Every rebuilt port must add `canonicalRecordId` and `traceId` so replay, mirrored projections, and action audits can be joined reliably.

If a message can contribute to a repo-governed `dialogue_emitted` or `group_turn_committed` outcome, it must also carry a portable `surfaceId`.

### 5.2 Private Message Contract `[CONFIRMED CORE + REBUILD POLICY]`

Portable fields:

- base message fields
- `fromUnit`
- `toUnit`
- `type`
- `point`
- `weather`

Code anchor:

- `OpenAIWorld.mod.data/PrivateMessageData.cs`

Implementation rule:

Persist one-to-one conversation turns with enough spatial context to later reconstruct what happened, where, and under what conditions.

### 5.3 Group Message Contract `[CONFIRMED CORE + REBUILD POLICY]`

Portable fields:

- base message fields
- `fromUnit`
- `toUnits[]`
- `point`
- `weather`

Code anchor:

- `OpenAIWorld.mod.data/GroupMessageData.cs`

Implementation rule:

The stored record must know who spoke and who the audience was, even if the rendered UI only shows a single line.

### 5.4 Contact Group Contract `[CONFIRMED CORE + REBUILD POLICY]`

Portable fields:

- `id`
- `name`
- `newMessageCount`
- `doNotDisturb`
- `units[]`

Code anchor:

- `OpenAIWorld.mod/ContactGroup.cs`

Implementation rule:

Remote group communication is a first-class channel, not a variant of local proximity chat.

### 5.5 Long Memory Contract `[CONFIRMED CORE + REBUILD POLICY + REPO OVERLAY]`

Portable rebuild fields:

- `memoryKey`
- `timeBucket`
- `bucketUnit`
- `sourceTurnId`
- `memoryOwnerId`
- `content`
- `summaryVersion`
- optional `recallSurfaceId`

Recovered storage anchor:

- `OpenAIWorld.mod/ExperienceData.cs`

Implementation rule:

Long memory is summary memory keyed by a time bucket, not an infinite raw transcript.

Source-backed storage minimum:

- recovered source directly anchors a bucket key (`roundMonth`) and a summary payload (`content`)
- portable `timeBucket` is the normalized join key rebuilt from that source bucket

Rebuild overlay note:

- fields such as `memoryKey`, `sourceTurnId`, `memoryOwnerId`, `summaryVersion`, and `recallSurfaceId` are rebuild-layer additions for replayability and repo-governed audit joins
- they are not claimed as original `ExperienceData` member names

Rebuild rule:

- source evidence anchors a month bucket
- each target port must choose one stable `bucketUnit` up front and keep it fixed per save
- default to `month`
- if the host pacing makes month nonsense, use `season` or another slower-than-day bucket, but document it as a host divergence
- keep one canonical summary per actor per closed `timeBucket` by default
- if interim recompression is needed, replace the previous summary for that same `timeBucket` and increment `summaryVersion`

Committed outcome rule:

These join fields are rebuild-layer replay requirements, not claims about the literal stored shape of `ExperienceData`.

- `memory_recorded` is not committed until the persistent memory ledger write succeeds
- `memory_recorded` must be joinable by `memoryKey + sourceTurnId + timeBucket + memoryOwnerId`
- if memory is later recalled into a visible turn, `memory_recalled` must carry `memoryKey + sourceTurnId + timeBucket + recallSurfaceId`

### 5.6 World Event Contract `[CONFIRMED CORE + REBUILD POLICY + REPO OVERLAY]`

Portable normalized fields:

- `eventTypeId`
- `eventId`
- `roundTotalDay`
- `content`
- `locationId`
- `locationDisplayName` or null
- `locationType` or null
- `point`
- `iconKey` or null
- `itemRefs[]`
- `participantActorIds[]`
- `addActorArchetypes[]`
- `lifecycleState`
- `eventState`
- `hostSurfaceRef`
- `affectedScope`
- `rollbackHandle`
- `skipOrFailureReason`

Normalization note:

`locationType` and `addActorArchetypes[]` are portability-layer normalized fields reconstructed from the recovered prompt payload shape plus host materialization needs.

They are not a claim that the original obfuscated materializer exposed those exact internal property names end to end.

Recovered storage anchor:

- `OpenAIWorld.mod/WorldEventData.cs`

Implementation rule:

World events are durable game objects with their own lifecycle, not just narrated flavor text.

Source-backed storage minimum:

- recovered source directly anchors numeric `id`, `eventType`, `roundTotalDay`, `content`, `locationName`, `soleID`, `point`, `icon`, `items`, `units`, and `triggered`

Rebuild overlay note:

- fields such as `locationId`, `locationType`, `addActorArchetypes[]`, `eventState`, `hostSurfaceRef`, `affectedScope`, `rollbackHandle`, and `skipOrFailureReason` belong to the portability/replay layer
- they are required for a reproducible cross-host rebuild, but they are not claimed as original `WorldEventData` member names

Rebuild naming rule:

- source `eventType` maps to portable `eventTypeId`
- source `soleID` maps to portable `eventId`
- source `locationName` is the recovered display/location string and must not be silently collapsed into canonical `locationId` without an adapter mapping rule
- source `id` is a separate recovered numeric field and should be treated as an adapter-local storage key or legacy row id, not collapsed into `eventTypeId`
- source `triggered` maps into the portable narrative lifecycle state machine
- source prompt field `addUnits` maps to portable `addActorArchetypes[]` as a host-factory request list and may normalize to `[]` when no new actor creation is requested
- source prompt field `locationType` is preserved when present and may normalize to `null` when the host does not distinguish location classes

#### 5.6.1 Narrative Lifecycle Contract

Only `triggered` is directly anchored in recovered storage/writeback.

The broader lifecycle machine below is a rebuild portability model so different hosts do not invent incompatible semantics.

Use this player-visible lifecycle state model:

- `proposed`
- `surfaced`
- `triggered`
- `resolved`
- `expired`
- `deleted`

Allowed transitions:

- `proposed -> surfaced`
- `surfaced -> triggered`
- `surfaced -> resolved`
- `surfaced -> expired`
- `triggered -> resolved`
- `triggered -> expired`
- any non-deleted state -> `deleted` only through host cleanup or save repair

#### 5.6.2 Commit State Contract

Track host apply outcome separately as `eventState`:

- `pending`
- `applied`
- `delayed`
- `skipped`
- `rolled_back`
- `failed`

Rules:

- a newly accepted proposal enters `eventState = pending`
- successful host materialization transitions it to `applied`
- legality gates or pacing gates may keep it at `delayed`
- validation rejection or safe non-apply must use `skipped`
- host rollback paths must use `rolled_back`
- hard apply failures must use `failed`

Repo-governed audit logs must preserve `eventState`, `rollbackHandle`, and `skipOrFailureReason` without collapsing them into the narrative lifecycle.

#### 5.6.3 World Event Proposal Contract

Every normalized top-level `worldEvent` payload must contain:

- `eventTypeId`
- `eventId` or null before persistence
- `headline`
- `content`
- `locationId`
- `locationDisplayName` or null
- `locationType` or null
- `participantActorIds[]`
- `addActorArchetypes[]`
- `itemRefs[]`
- `iconKey` or null
- `preconditions[]`
- `desiredInitialState` defaulting to `surfaced`
- `eventState` defaulting to `pending`
- `affectedScope`
- `rollbackHandle`
- `skipOrFailureReason`

Normalization rule:

- the model-authored `WORLD_EVENT_PROPOSAL.args` may omit host-derived fields such as `eventId`, `iconKey`, `eventState`, `affectedScope`, `rollbackHandle`, and `skipOrFailureReason`
- the deterministic layer must either derive those fields explicitly or materialize `null`/default values before persisting the top-level normalized `worldEvent`

## 6. Private Dialogue Reproduction Path

### 6.1 What Is Confirmed

Confirmed by code:

- private messages are stored in a dedicated data structure
- each persisted turn includes sender, target, day, timestamp, point, weather, type, and role/content
- the response path parses model output into JSON and a list of typed actions before invoking the downstream callback
- private chat context assembly includes current place/weather, speaker summary, target relation context, replayed private history, and the current input
- the system stores a sidecar JSON blob by message index in addition to the visible message

Key anchors:

- `OpenAIWorld.mod.data/PrivateMessageData.cs`
- obfuscated private-chat manager at `.../F.cs:13321-13350`
- obfuscated response parse path at `.../F.cs:13817-13838`
- obfuscated request-build path at `.../F.cs:13701-13800`
- obfuscated persistence and sidecar storage at `.../F.cs:13497-13517`

### 6.2 Rebuild It This Way `[REBUILD POLICY]`

1. Create a `PrivateDialogueOrchestrator`.
2. Input:
   - actor snapshot
   - target snapshot
   - relation snapshot from actor to target
   - current scene snapshot
   - recent private history between the two
   - optional long-memory summary if the rebuild chooses to reinject compressed memory
   - optional player utterance or trigger text
3. Prompt bundle:
   - world rules
   - private dialogue channel rules
   - behavior protocol
   - propagation protocol
4. Force JSON response mode when provider supports it.
5. Run JSON repair if parsing fails.
6. Parse:
   - rendered text
   - `actions[]`
   - optional propagation intents
7. Validate actions against host whitelist.
8. Persist mirrored message records for both sides. If the host uses one physical store, create equivalent actor-local indices or projections so both participants can replay the exchange from their own history view.
9. Persist the canonical replay envelope plus actor-local projections.
10. Apply deterministic actions.
11. Feed accepted outcomes into future memory compression.

### 6.3 Non-Negotiable Reproduction Requirements

- separate dialogue generation from host execution
- keep environmental metadata with the message
- make relation-sensitive context actor-relative, not globally symmetric
- log parse failures instead of silently dropping them

## 7. Group Chat Reproduction Path

### 7.1 What Is Confirmed

Confirmed by code or prompts:

- group chat has its own persisted message type
- remote contact-group chat is a different channel with its own storage bucket keyed by `ContactGroup.id`
- group messages include sender, audience array, place, and weather
- there is a dedicated speaking-order guidance prompt
- group chat history is replayed from stored records in the UI/controller layer
- the runtime uses a two-stage pattern: first plan an ordered speaker list, then generate one reply per selected speaker
- each group or contact-group reply can emit structured effects that are applied immediately after the reply
- stored group/contact-group messages are mirrored back into each participant's private history as synthetic `PrivateMessageData`

Anchors:

- `OpenAIWorld.mod.data/GroupMessageData.cs`
- `OpenAIWorld.mod.data/ContactGroupMessageData.cs`
- `OpenAIWorld.mod/ContactGroup.cs`
- `decoded_prompts/群聊.md`
- `decoded_prompts/群聊_发言顺序引导.md`
- `decoded_prompts/传音群聊.md`
- `decoded_prompts/传音群聊_发言顺序引导.md`
- obfuscated group-history render path at `.../F.cs:11925-11959`

### 7.2 Rebuild It This Way `[REBUILD POLICY]`

1. Create a `GroupChatOrchestrator`.
2. Split it into two substeps:
    - `SpeakerSelectionStep`
    - `PerSpeakerGenerationStep`
3. Speaker selection input:
   - scene
   - participant set
   - recent group history
   - topic seed
4. Use a dedicated speaking-order prompt or deterministic selector.
5. Freeze the selected speaker list for the current round before generating the first line.
6. For each selected speaker, in order:
    - assemble speaker-centric context
    - include relations to the other visible participants
    - optionally include speaker long-memory summary if the rebuild enables summary-memory reinjection
    - include accepted earlier turns and accepted earlier deterministic effects from this same round
    - ask for one turn only
7. Persist each turn as a separate `GroupMessageRecord`.
   - every persisted group turn must carry `groupTurnId`, `sequenceIndex`, and `surfaceId`
8. Parse and apply structured effects per generated turn before moving to the next speaker.
9. Mirror the delivered turn into each participant's private history projection as synthetic private records for faithful reproduction.
10. Use the same two-stage engine for both local group chat and remote routed communication; only the carrier surface and routing rules should differ.

#### 7.2.1 Required Speaker Selection Contract

The selection step must emit:

- `orderedSpeakerActorIds[]`
- `selectionReason`
- `selectionTrace`

Use these deterministic rules:

- faithful default maximum selected speakers per round is `min(visible participants, 6)` because both recovered scheduler prompts cap the output at six speakers
- remove duplicates while preserving first occurrence
- discard unknown actor ids
- discard actors failing `canSpeak`
- if the resulting list is empty, fall back to the deterministic selector
- deterministic fallback orders candidates by `canSpeak`, scene presence, recent silence, then stable `actorId`

Ports may lower this cap only as an explicit host divergence documented in the host profile.

This does not claim the original code used this exact heuristic.

It defines the rebuilt contract so different ports do not invent incompatible schedulers.

### 7.3 Why This Separation Matters

The original prompt assets imply that speaking order is not just emergent from one big all-speakers generation call.

That pattern is worth preserving because it avoids:

- all speakers sounding the same
- one model output controlling the full room
- impossible per-speaker memory isolation

## 8. Long Memory Reproduction Path

### 8.1 What Is Confirmed

Confirmed by code:

- the stored summary object is `ExperienceData { roundMonth, content }`
- the summary is actually generated by an LLM compression pass over one month of private messages and logs
- summary data can be exported and imported as serialized lists
- the UI/controller can enumerate all summaries for a unit and display or delete them
- monthly labels and message/log counts are used around this subsystem

Anchors:

- `OpenAIWorld.mod/ExperienceData.cs`
- obfuscated compressor and storage manager in `.../g.cs:10625-10803`
- obfuscated memory controller at `.../s.cs:11527-11556`
- export/import paths at `.../s.cs:11687-11723`
- summary list render path at `.../s.cs:12135-12147`

Confirmed by prompts:

- a dedicated memory-compression prompt exists and expects raw chat/events/old memory as input, then emits compressed memory text

Anchor:

- `decoded_prompts/记忆压缩.md`

### 8.2 Safest Reproduction Design `[REBUILD POLICY]`

Implement long memory as:

- raw recent history window
- month-bucketed summary memory

Recovered code proves compression, storage, import/export, and UI display of summary memory.

Recovered code does not yet prove that `ExperienceData` is reinjected into every runtime generation path.

Therefore:

- summary compression and storage are faithful-core requirements
- summary reinjection into future prompts is a recommended reconstruction option, not a recovered fact

Recommended flow:

1. Keep recent raw history per channel.
2. At host-defined intervals, gather:
   - selected chat history
   - important event records
   - existing memory summaries for the actor
   - host logs or equivalent world-facing records if your host has them
3. Call the memory-compression prompt.
4. Store one canonical summary string under the configured bucket, replacing older interim summaries for that same actor/bucket and incrementing `summaryVersion`.
5. Optionally use those summaries later as actor-relative memory context if the target port wants compressed-memory recall.

### 8.3 What To Preserve

- actor ownership of memory
- time buckets
- strong compression bias
- separation between audit log and cognitive memory

### 8.4 What Is Optional Reconstruction, Not Recovered Fact

- feeding `ExperienceData` summaries back into private dialogue prompts
- feeding `ExperienceData` summaries back into group-chat prompts
- making summary-memory reuse mandatory in the verification checklist

## 9. Information Propagation Reproduction Path

### 9.1 What Is Confirmed

Confirmed by prompts:

- propagation is a distinct action protocol, not free-form dialogue
- it is explicitly meant for messaging non-current participants
- it allows selective, delayed, biased, or motive-driven forwarding

Anchor:

- `decoded_prompts/信息裂变.md`

Supported by recovered structure:

- contact groups exist as a dedicated data model
- private and group message storage are separate
- action parsing infrastructure already expects parsed action lists
- group/contact-group channels mirror delivered messages back into participant history

Anchors:

- `OpenAIWorld.mod/ContactGroup.cs`
- `OpenAIWorld.mod.data/PrivateMessageData.cs`
- `OpenAIWorld.mod.data/GroupMessageData.cs`
- `OpenAIWorld.mod.data/ContactGroupMessageData.cs`
- obfuscated action parse paths in `.../F.cs`

### 9.2 Rebuild It This Way `[REBUILD POLICY]`

Do not model propagation as append-only lore text.

Model it as a real deliverable action:

1. model proposes `ConveyMessage`
2. deterministic layer resolves target set, reachability, and allowed carrier surfaces
3. deterministic router chooses one carrier:
    - local face-to-face
    - remote direct one-to-one contact
    - one-off broadcast carrier such as bulletin, notification, or letter
    - deferred inbox/log if immediate delivery is impossible
4. resulting delivered message becomes a real message record for the receiver's future context

Propagation is not a standalone free-text conversation channel.

It is a dedicated protocol execution path triggered by accepted `CONVEY_MESSAGE` actions from dialogue, group-chat, or remote-routed communication turns.

It must not redefine the dedicated `remote routed communication` channel as just another propagation carrier.

Source-accurate clarification:

- the recovered readable direct router already anchors a one-target split between:
  - local private dialogue
  - remote direct one-to-one communication
- broader carriers such as one-off broadcast, bulletin, notification, letter, or deferred inbox remain reproduction-layer generalizations unless stronger source anchors are found
- kin/family rescue is one valid propagation use-case, not the definition of the propagation subsystem

#### 9.2.1 Required Strategy Hooks

Recovered evidence does not fully expose the exact original branch chooser.

Do not present a guessed router as recovered fact.

What every faithful rebuild must do is publish the routing strategy explicitly and keep it deterministic.

Every port must define these strategy hooks:

- `ResolveTargets(propagationIntent, HostActionContext) -> targetActorIds[]`
- `ResolveReachability(targetActorId, SceneSnapshot, WorldClockSnapshot) -> local | remote | unreachable`
- `ResolveRemoteCarrier(targetActorId, propagationIntent) -> direct | bulletin | notification | deferred`
- `CanDeliverNow(carrier, hostLegalityTags, worldStateTags) -> bool`
- `CreateDeliveryRecord(carrier, senderActorId, receiverActorIds, content, metadata) -> persisted record ids`
- `QueueDeferredDelivery(...) -> deferred delivery id`

Reference default strategy profile:

If a port does not ship a custom profiled strategy, use this branch order:

1. dedupe targets
2. reject targets over `MaxPropagationTargets`
3. reject any target already seen in the current propagation trace
4. if target is locally reachable and can speak now, use `local face-to-face`
5. else if a legal direct remote carrier exists, use `remote direct one-to-one contact`
6. else if a legal one-off broadcast carrier exists, use `bulletin | notification | letter`
7. else create deferred delivery

Reference anti-explosion defaults:

- persist `originTraceId` on every propagation-derived delivery
- maintain `hopCount`
- default `MaxPropagationTargets = 3`
- default `MaxPropagationHops = 1`
- do not deliver the same normalized content hash twice to the same receiver within the same world-day bucket

Ports may override carrier taxonomy or cap values only through a documented host profile.

What is invariant is that propagation must resolve targets, choose or defer a carrier deterministically, persist receiver-visible records, and emit replayable lineage fields.

If a port wants a multi-speaker remote room, that must be implemented through the dedicated `remote routed communication` channel and its speaker scheduler, not by treating a propagation delivery as a room substitute.

The recovered source confirms that direct private-message storage and contact-group storage both exist.

It does not force every new host to expose a literal `ContactGroup` object.

What is mandatory is the semantic outcome: propagation must be routed deterministically into persisted receiver-visible communication state.

### 9.3 Main Reproduction Rule

Propagation must create new context for other actors.

If no new actor-visible record is created, you did not actually reproduce the feature.

## 10. Active World Reproduction Path

### 10.1 What Is Confirmed

Confirmed by data model and code:

- world events are stored durably as `WorldEventData`
- active-world scheduling exists in obfuscated world logic and is gated by auto-run settings
- model output is parsed, sanitized, and materialized into persistent events
- map-event interaction later marks the corresponding `WorldEventData.triggered = true`
- the world-generation pipeline can create missing NPCs and bind items into the event
- previous world events are fed back into later world-generation context

Anchors:

- `OpenAIWorld.mod/WorldEventData.cs`
- active-world scheduler and storage in `.../O.cs:12899`, `.../O.cs:13204`, `.../O.cs:13238`, `.../O.cs:13569`, `.../O.cs:13624`, `.../O.cs:13663`
- triggered writeback in `.../E.cs:11707-11718`
- feedback loop in `.../O.cs:13481`

Confirmed by prompts:

- separate world-rules and world-event prompts exist
- world-event generation expects structured JSON with content, items, units, addUnits, location, and location type

Anchors:

- `decoded_prompts/世界定义.md`
- `decoded_prompts/世界推演.md`

### 10.2 Rebuild It This Way `[REBUILD POLICY]`

1. Create a `WorldDirectorOrchestrator`.
2. Trigger it from world progression plus host legality gates, not only player clicks.
3. Inputs:
    - current world time
    - recent world events
    - candidate actors and locations
    - current world rules bundle
4. Prompt bundle:
   - world rules
   - world-event generation rules
5. Parse JSON into a `WorldEventProposal`.
6. Deterministic layer:
    - validate locations
    - resolve participating actors or request host-native actor factories only through registered archetype/content pipelines
    - resolve items or request host-native item/content factories only through registered content pipelines
    - allocate icon/type
    - persist a durable event object with both narrative `lifecycleState` and host apply `eventState`
7. Expose the durable event object through the host's legal event surface such as incidents, quests, encounters, site objects, or map events.
8. Mark `triggered` or equivalent lifecycle flags when the host-visible event surface is actually opened or interacted with.

### 10.3 Main Reproduction Rule

Active world means the world can gain new durable event objects without a direct player line of dialogue causing them that instant.

If your world AI only writes flavor text into a log, it is not the same subsystem.

## 11. Action And Validation Contract

### 11.1 Required Portable Schema `[REBUILD POLICY]`

Source-faithful model-output contract to preserve from OpenAIWorld:

- `content`
- `actions[]`
- optional parse/diagnostic sidecar

Repo-governed adapter overlay:

- when `post-M1-platformize` is explicitly entered in this repository, the parser may additionally normalize the source-faithful result into framework-facing `action_intents[]`, `propagation_intents[]`, `world_event_intents[]`, and `diagnostic_sidecar[]`
- this overlay is an adapter bridge into the repo framework, not a claim that the original mod emitted those field names

OpenAIWorld-faithful replay/debug normalization may then project source or repo-adapted results into:

- `content`
- `actions[]`
- `propagation[]`
- `worldEvent`
- parse diagnostics

Canonical rule:

- outside repo integration, `actions[]` remains the source-faithful model-facing side-effect list
- inside `Phase A: source-faithful reproduction`, `actions[]` remains the authoritative accepted side-effect list
- inside `Phase B: repo overlay`, `actions[]` may be bridged into framework-facing `action_intents[]` before deterministic validation
- `propagation_intents[]` and `world_event_intents[]` exist only at the repo adapter boundary
- `propagation[]` and `worldEvent` are replay/debug convenience projections produced by the deterministic layer after intent parsing
- the model should not be asked to emit the same side effect twice in both places
- if a provider or legacy prompt path emits both repo-governed intent fields and source-style aliases, the adapter must reconcile them before deterministic validation and log any mismatch
- canonical normalized envelopes always materialize both top-level keys, using `[]` and `null` when absent

Use these compatibility response envelopes after normalization:

For brevity, the remaining subsections use `actions[]` as the normalized portable action list after any repo adapter bridging has completed.

`DialogueResult`

```json
{
  "content": "string",
  "actions": [],
  "propagation": [],
  "worldEvent": null,
  "diagnostics": []
}
```

`SpeakerSelectionResult`

```json
{
  "orderedSpeakerActorIds": ["npc_a", "npc_b"],
  "selectionReason": "string",
  "selectionTrace": []
}
```

`WorldEventResult`

```json
{
  "content": "string",
  "actions": [],
  "propagation": [],
  "worldEvent": {},
  "diagnostics": []
}
```

Interpretation rule:

- `DialogueResult.actions[]` may contain `CONVEY_MESSAGE`
- `DialogueResult.propagation[]` is the deterministic projection of those `CONVEY_MESSAGE` actions after normalization
- `WorldEventResult.actions[]` may contain `WORLD_EVENT_PROPOSAL`
- `WorldEventResult.worldEvent` is the deterministic projection of that `WORLD_EVENT_PROPOSAL` action after normalization
- if no such action is present, the corresponding top-level projection must be empty or null

#### 11.1.1 Portable Action Object Contract `[REBUILD POLICY]`

Every `actions[]` entry must normalize into:

- `actionId`
- `actionType`
- `sourceActorId`
- `targetActorIds[]`
- `channelType`
- `args`
- `preconditions[]`
- `requestedCarrier` if the action is message or propagation related
- `traceId`

`actionType` must refer to a registry entry owned by the deterministic layer, not a raw host method name.

`args` must be JSON-serializable and validated against the registry schema before any host call is attempted.

#### 11.1.2 Action Registry Boundary `[REBUILD POLICY]`

Implement a registry like:

- `ActionRegistry.Register(actionType, validator, hostIntentBuilder, allowlistTags[])`
- `Validate(action, HostActionContext) -> accepted | rejected + reason`
- `BuildHostIntent(action) -> host-native intent`
- `ApplyHostIntent(intent) -> ActionResult`

This is the portability boundary.

The model may only request registered action types.

The registry decides how that action becomes a host-native legal intent.

#### 11.1.3 Canonical Minimum Action Vocabulary `[REBUILD POLICY]`

Every faithful port must implement at least these action types:

- `RELATION_DELTA`
- `ITEM_TRANSFER_OFFER`
- `STATE_TAG_ADD`
- `STATE_TAG_REMOVE`
- `INVITATION`
- `MOVE_INTENT`
- `CONVEY_MESSAGE`
- `WORLD_EVENT_PROPOSAL`

Repo-governed extension required in this repository:

- `TRANSACTION_STATE_UPDATE`

This extension exists so deterministic lowering can emit `transaction_state_committed` across `offer`, `accept`, `reject`, `counter`, `obligation outstanding`, and `fulfillment` states.

It is a repo integration requirement, not a claim that the original OpenAIWorld prompt corpus used this exact literal action name.

Required `args` contracts:

`RELATION_DELTA`

- `metric`
- `delta`
- `reason`

`ITEM_TRANSFER_OFFER`

- `itemRef`
- `quantity`
- `offerMode`

`TRANSACTION_STATE_UPDATE`

- `transactionId`
- `offererId`
- `counterpartyId`
- optional `brokerId`
- `state`
- `resourceOrServiceKey`
- `targetScope`

`STATE_TAG_ADD` and `STATE_TAG_REMOVE`

- `stateTag`
- `durationHint`

`INVITATION`

- `inviteType`
- `destinationId`
- `timeHint`

`MOVE_INTENT`

- `destinationId`
- `urgency`

`CONVEY_MESSAGE`

- `content`
- `intentTags[]`
- `preferredCarrier`
- `sourceFactId` or `sourceEventId`

`WORLD_EVENT_PROPOSAL`

- `eventTypeId`
- `eventId` or null
- `headline`
- `content`
- `locationId`
- `locationType` or null
- `participantActorIds[]`
- `addActorArchetypes[]`
- `itemRefs[]`
- `preconditions[]`
- `desiredInitialState`

Host-derived enrichment rule:

- the deterministic layer may enrich the normalized top-level `worldEvent` with `iconKey`, `eventState`, `affectedScope`, `rollbackHandle`, and `skipOrFailureReason`
- if `eventId` is omitted by the model, the deterministic layer must mint or allocate it before persistence

Any source-accurate extra action types may be added later, but ports must not change the shape of this minimum vocabulary.

#### 11.1.4 Projection Rules `[REBUILD POLICY]`

Use these projection rules after action validation:

- each accepted `CONVEY_MESSAGE` action projects into one `PropagationIntent`
- at most one accepted `WORLD_EVENT_PROPOSAL` action may appear per result envelope
- that accepted `WORLD_EVENT_PROPOSAL` projects into the top-level `worldEvent`
- rejected actions do not project into top-level fields
- top-level `propagation[]` and `worldEvent` are replay/debug helpers, not parallel authoring contracts

#### 11.1.5 PropagationIntent Contract `[REBUILD POLICY + REPO OVERLAY]`

Each projected `PropagationIntent` must normalize into:

- `propagationId`
- `sourceFactId` or `sourceEventId`
- `channelType`
- `deliveryMode`
- `deliveryState`
- `targetScope`
- `receiverActorIds[]`
- `content`
- `preferredCarrier`
- `originTraceId`
- `hopCount`

`propagation[]` is the replay/debug surface for committed `propagation_committed` outcomes.

### 11.2 Required Deterministic Checks

For each action:

- action type exists in host whitelist
- actor exists
- target exists if required
- distance/proximity rules are satisfied
- resource requirements are satisfied
- relation or state gates are satisfied
- duplicate or impossible actions are rejected cleanly

Also require:

- action type is allowed for this channel
- action args satisfy the registry schema
- host-intent builder resolves to a legal host-native path instead of raw field mutation
- for `CONVEY_MESSAGE`, recipient targeting must come from the action object's top-level `targetActorIds[]`; args must not redefine it

#### 11.2.1 Per-Channel Allowlist

Maintain an explicit channel allowlist table.

Minimum expectation:

- private dialogue: relation shifts, direct item offers, invitations, local movement intents, propagation proposals
- local group chat: relation shifts, emotes, lightweight social effects, propagation proposals
- remote routed communication: relation shifts, information delivery, invitation or follow-up proposals
- active world: world-event proposals only

If an action is not explicitly allowed for the channel, reject it even if the registry knows the action type.

### 11.3 Required Failure Semantics

- malformed JSON: repair, retry parse, or reject with trace
- unknown action: reject action, keep text if safe
- impossible host apply: reject action, log reason
- world-event apply failure: persist `eventState = skipped | failed | rolled_back` with reason, do not poison whole turn

This text-can-survive-even-if-an-action-fails pattern is the safest cross-host reproduction choice.

#### 11.3.1 Required Action Result And Audit Record `[REBUILD POLICY + REPO OVERLAY]`

Persist one action audit record per attempted action.

Portable core fields:

- `traceId`
- `messageOrEventId`
- `actionId`
- `actionType`
- `sourceActorId`
- `targetActorIds[]`
- `status` as `accepted`, `rejected`, `applied`, `failed`
- `reasonCode`
- `normalizedArgs`
- `hostIntentSummary`
- `appliedEffectsSummary`
- optional committed outcome payload
- `timestamp`

Repo-governed overlay fields:

- `traceId`
- `requestId`
- `launchSessionId`
- `skuId`
- `gameId`
- `billingSource`
- `channelType`
- `capability`
- `narrativeTurnId`
- `executionResult`
- `degradedMode`
- `traceGroupId`
- `claimStateRef`
- `claimStateAtEvent`
- `degradationWindowId`
- `degradationStartedAt`
- `evidenceReviewRef`
- conditional `escalationDeadlineAt`
- conditional `escalatedAt`
- conditional `waiverId`
- conditional `waiverLineageId`
- conditional `recoveredAt`
- conditional `recoveryEvidenceRef`
- optional committed outcome payload such as:
  - `memoryKey`, `sourceTurnId`, `timeBucket`, `memoryOwnerId`, `recallSurfaceId`
  - `transactionId`, `offererId`, `counterpartyId`, `brokerId`, `state`, `resourceOrServiceKey`, `targetScope`
  - `groupTurnId`, `sequenceIndex`, `surfaceId`
  - `propagationId`, `sourceFactId`, `sourceEventId`, `deliveryMode`, `deliveryState`, `targetScope`
  - `eventId`, `eventType`, `eventState`, `affectedScope`, `rollbackHandle`, `skipOrFailureReason`
  - `traceId`, `recoveryEntry`, `reasonCode`

The sidecar/audit store must be replayable without re-querying the model.

#### 11.3.2 Required Visible Persistence Layout

For each generated dialogue turn, persist:

- one canonical replay envelope
- one visible channel record in the channel's native store
- zero or more mirrored actor-local projections
- one action audit record per attempted action
- one prompt-resolution manifest

For each world-event proposal, persist:

- one canonical replay envelope
- one world-event object or rejection record
- one action audit record for the proposal path
- one prompt-resolution manifest

For repo-governed implementations, these persisted records must be sufficient to deterministically emit:

- `dialogue_emitted`
- `memory_recorded`
- `memory_recalled`
- `transaction_state_committed`
- `group_turn_committed`
- `propagation_committed`
- `world_event_committed`
- `recovery_instruction`

`recovery_instruction` is a repo-governed runtime outcome and may be emitted by the deterministic/runtime governance layer even when it was not model-authored as an OpenAIWorld-style action.

#### 11.3.3 Required Completion Gates

Do not emit any `*_committed` outcome merely because a model response was parsed or an intent was accepted.

Completion gates:

- `dialogue_emitted` only after the player-visible dialogue surface has completed rendering and a portable `surfaceId` is known
- `memory_recorded` only after the persistent memory ledger write succeeds
- `memory_recalled` only after the recalled memory is bound to a visible surface and can be joined back to `sourceTurnId + timeBucket`
- `transaction_state_committed` only after transactional persistence completes with actor roles and state written durably
- `group_turn_committed` only after the committed group turn has a persisted `groupTurnId + sequenceIndex + surfaceId`
- `propagation_committed` and `world_event_committed` only after an explicit committed state payload has been written
- `recovery_instruction` only after the recovery entry is persisted, trace-linked, and bound to a visible or operator-actionable recovery surface

Before a completion gate is met, the audit trail may record `accepted`, `rejected`, `applied`, or `failed`, but it must not claim the corresponding shared outcome has been committed.

#### 11.3.4 Repo-Governed M1 Lowering Rule

When this reproduction is implemented inside this repository under `M1` constraints:

- lowering may target only `render_command` and `transactional_command`
- ports must not introduce new shared command classes merely to satisfy propagation or active-world semantics
- heavier lowering for `group_chat`, `information_propagation`, and `active_world` belongs only to `M2+` or an approved experiment annex

## 12. Minimum Host Interface Contract

Any target game must expose the following host APIs or equivalent adapters.

### 12.1 Identity And Mapping APIs

- resolve actor by canonical AI id
- resolve location by canonical AI id
- resolve event by canonical AI id
- maintain mapping from canonical AI ids to ephemeral host handles when native ids are unstable

### 12.2 Actor And Relation Read APIs

- enumerate visible or relevant actors
- inspect actor tags, state, schedule/duty summary, and inventory summary
- inspect actor-relative relation view against another actor

### 12.3 Scene And Reachability APIs

- resolve current location or room
- resolve proximity
- resolve local visibility
- resolve remote reachability or communication availability
- resolve environmental context if available

### 12.4 Time And Legality APIs

- current day
- current month/season/summary bucket
- world progression hook
- legality gate callback for whether an AI action or world event can surface now

### 12.5 Action Intent APIs

- translate whitelisted action types into host-native legal intents
- apply relation, inventory, faction, state, travel, or invitation intents through host rules
- reject unsafe or illegal intents with structured reason codes

### 12.6 Messaging Persistence APIs

- persist direct messages
- persist local group messages
- persist remote routed messages through one or more host-native multi-speaker room/thread surfaces
- mirror delivered communication into actor-local replay history projections
- persist sidecar/audit records keyed to messages or deliveries

### 12.7 World Event APIs

These are host-adapter requirements for the rebuilt portability layer, not a claim that the original source exposed identical API boundaries or identical stored members.

- create a durable event object with lifecycle state
- bind participants or actor references
- bind items or content references
- surface the event through host-native legal systems
- mark triggered/resolved/expired/deleted

## 13. Implementation Order

Follow this order unless the target host is extremely unusual.

### Phase 1

- implement data contracts
- implement canonical id mapping and snapshot normalization
- implement host snapshot adapters
- implement provider config and transport
- implement prompt bundle resolution with explicit precedence
- implement action registry and audit schema
- implement JSON repair and parse layer

### Phase 2

- implement private dialogue end to end
- persist `PrivateMessageRecord`
- validate and apply a minimal action whitelist

### Phase 3

- implement long-memory compression
- restore recovered-style summary-memory reinjection after the compression store is stable

### Phase 4

- implement group chat with speaker-order step
- persist `GroupMessageRecord`
- mirror each delivered line into participant private-history projections

### Phase 5

- implement information propagation as real message delivery
- implement remote routed communication carrier selection
- implement at least one host-native remote carrier and one deferred carrier

### Phase 6

- only after Phases 1-5 have passed source-faithful verification, add repo overlay:
  - `action_intents[]`
  - `diagnostic_sidecar[]`
  - hosted truth redistribution
  - shared platform abstractions

### Phase 6

- implement active world event generation
- persist `WorldEventRecord`
- surface world events through the host

Only after all six phases should you start decorative expansion such as richer item generation or host-specific polish.

## 14. Verification Checklist

The system is not ported unless all items below pass.

### Private Dialogue

- actor and target each receive persisted chat history
- generated actions are parsed separately from text
- failed actions do not crash the whole turn

### Long Memory

- raw history and summary memory are separate stores
- summary generation is bucketed by time
- if summary-memory reinjection is enabled in the port, future turns can consume summary memory

### Group Chat

- speakers are selected explicitly
- each line is attributable to a single speaker
- group history persists and can be replayed

### Propagation

- a propagation action creates new receiver-visible state
- receiver future context changes because of it

### Active World

- new world events can appear without immediate player click-to-speak
- event objects persist and have lifecycle state

### Cross-Cutting

- all model outputs go through repair/parse/validation
- every accepted host mutation is auditable
- inference-only assumptions are not hardcoded as recovered truth

## 15. Final Reproduction Rules

If you want a faithful, portable reproduction, keep these rules:

1. Copy the architecture, not the shell.
2. Separate channels.
3. Separate raw history from compressed memory.
4. Keep actions structured and deterministic.
5. Treat propagation as actual message delivery.
6. Treat active world as event creation, not just narration.
7. Replace setting-specific fiction with host-native fiction.
8. Never promote inference to fact without code support.

## 16. Known Limits Of This Manual

The following parts are intentionally deferred to appendices because current code evidence is incomplete:

- exact obfuscated class names for every subsystem owner
- exact original action-dispatch table for every supported command
- exact original propagation routing chooser
- exact original prompt override precedence in every runtime path

Those limits do not block a faithful architecture reproduction.

They only block claims about exact one-to-one implementation identity.
