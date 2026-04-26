# Appendix A: Code Evidence Map

> Current-architecture interpretation note:
>
> This appendix indexes recovered code facts from the original mod.
> It anchors what the chain did and who owned it in the original source, but current hosted-vs-local placement is decided by the active framework docs, not by this evidence index alone.

## Evidence Rules

Status labels used here:

- `confirmed by code`
- `supported by asset`
- `inference`
- `unresolved`

Confidence labels:

- `high`
- `medium`
- `low`

## 1. Bootstrap And Hook Topology

### 1.1 Loader shell

- Claim: public `Loader` is only a wrapper around a deeper runtime owner.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `recovered_mod/GGBH_OpenAIWorld_20260326/decompiled/GGBH_OpenAIWorld_src/OpenAIWorld/Loader.cs:9-23`

### 1.2 Real bootstrap owner

- Claim: obfuscated singleton `Y.S` in `s.cs` is the actual post-loader bootstrap/service registry.
- Status: `inference`
- Confidence: `medium`
- Evidence:
  - `.../s.cs:10500`
  - `.../s.cs:10513`
  - `.../s.cs:10595`
  - `.../s.cs:11172-11190`

### 1.3 Real shutdown owner

- Claim: `Y.S.d()` handles runtime shutdown, event unsubscription, and module cleanup.
- Status: `inference`
- Confidence: `medium`
- Evidence:
  - `.../s.cs:11193`

### 1.4 Patched lifecycle domains

- Claim: the mod hooks data init, world init, world entry, day progression, world run, unit action creation, unit add, map-event lifecycle, UI manager behavior, and AI goal execution.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `.../OpenAIWorld/PatchClass.cs:41`
  - `.../OpenAIWorld/PatchClass.cs:54`
  - `.../OpenAIWorld/PatchClass.cs:75`
  - `.../OpenAIWorld/PatchClass.cs:88`
  - `.../OpenAIWorld/PatchClass.cs:140`
  - `.../OpenAIWorld/PatchClass.cs:146`
  - `.../OpenAIWorld/PatchClass.cs:169`
  - `.../OpenAIWorld/PatchClass.cs:232`
  - `.../OpenAIWorld/PatchClass.cs:274`
  - `.../OpenAIWorld/PatchClass.cs:332`
  - `.../OpenAIWorld/PatchClass.cs:342`
  - `.../OpenAIWorld/PatchClass.cs:352`
  - `.../OpenAIWorld/PatchClass.cs:362`
  - `.../OpenAIWorld/PatchClass.cs:433`
  - `.../OpenAIWorld/PatchClass.cs:443`
  - `.../OpenAIWorld/PatchClass.cs:547`
  - `.../OpenAIWorld/PatchClass.cs:587`
  - `.../OpenAIWorld/PatchClass.cs:633`

### 1.5 Common hook contract

- Claim: obfuscated base contract in `F.cs` exposes standardized module lifecycle and hook methods for the bootstrap owner and patches to fan into.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../F.cs:11470`
  - `.../F.cs:11558`

## 2. LLM Transport And Session Layer

### 2.1 Session abstraction

- Claim: there is a local session object with ordered messages, tool registry, and arbitrary custom data.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `.../OpenAIWorld.ailm/LLMSession.cs:21-112`

### 2.2 Chat-completion contract

- Claim: requests can include `response_format`, messages, and tool definitions; responses can include `tool_calls`.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `.../OpenAIWorld.ailm/ChatCompletions.cs:9-105`

### 2.3 Provider configuration

- Claim: provider configuration includes API URL, key, model, user agent, max tokens, temperature, top-p, proxy, JSON support, and thinking support flags.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `.../OpenAIWorld.mod/AIServer.cs`

### 2.4 Agent/server registry

- Claim: agent configuration is separate from server configuration.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `.../OpenAIWorld.mod/AIAgents.cs`

### 2.5 AI host/session orchestration owner

- Claim: obfuscated `Y.cs` namespace `n` owns AI server selection and chat-completion orchestration.
- Status: `inference`
- Confidence: `medium`
- Evidence:
  - `.../Y.cs:11209`
  - `.../Y.cs:11534`
  - `.../Y.cs:11565`
  - `.../Y.cs:11584`
  - `.../Y.cs:11689`

### 2.6 Prompt manager behavior

- Claim: prompts are resolved through an obfuscated manager that can load writable overrides, fall back to bundled assets, export bundled prompts, and apply runtime placeholder substitution.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../O.cs:12717`
  - `.../O.cs:12791`
  - `.../O.cs:12813`
  - `.../s.cs:9480`

## 3. JSON Repair And Structured Parsing

### 3.1 JSON repair is expected runtime behavior

- Claim: the project bundles a full JSON repair utility rather than assuming valid model JSON.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `.../jsonrepair/JsonRepair.cs`

### 3.2 Private chat response parsing

- Claim: private chat response handling parses the first model message into JSON, extracts content, converts action objects into a typed list, then filters that list before invoking downstream callbacks.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../F.cs:13817-13838`

### 3.3 World-event response parsing

- Claim: active-world response handling parses model JSON, sanitizes core fields, then materializes map/world events, units, and items.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../O.cs:12965`
  - `.../O.cs:12971`
  - `.../O.cs:13297`
  - `.../O.cs:13569`
  - `.../O.cs:13585`
  - `.../O.cs:13616`
  - `.../O.cs:13624`
  - `.../O.cs:13663`

## 4. Message Persistence Contracts

### 4.1 Base message contract

- Claim: all channel messages share index, day, datetime, and a `RequestMessage`.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `.../OpenAIWorld.mod.data/MessageData.cs:5-13`

### 4.2 Private message contract

- Claim: private messages persist sender, target, type, point, and weather.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `.../OpenAIWorld.mod.data/PrivateMessageData.cs:3-13`

### 4.3 Group message contract

- Claim: group messages persist sender, audience array, point, and weather.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `.../OpenAIWorld.mod.data/GroupMessageData.cs:3-11`

### 4.4 Contact-group message contract

- Claim: contact-group messages are modeled separately from local group messages.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `.../OpenAIWorld.mod.data/ContactGroupMessageData.cs:3-6`

## 5. Private Dialogue Chain

### 5.1 Mirrored message writeback

- Claim: one private exchange is written as paired `PrivateMessageData` records for both participants.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../F.cs:13321-13350`
  - `.../F.cs:13620`
  - `.../F.cs:13667`
  - `.../Y.cs:12752`
  - `.../Y.cs:13005`
  - `.../Y.cs:13046`

### 5.2 Environmental metadata retention

- Claim: each private message stores day, time, point, and weather at write time.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../F.cs:13321-13350`
  - `.../OpenAIWorld.mod.data/PrivateMessageData.cs:3-13`

### 5.3 Request composition

- Claim: private chat request composition injects place/weather for both sides, speaker profile, target relation block, replayed private history, and current input.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../F.cs:13701`
  - `.../F.cs:13710`
  - `.../F.cs:13768`
  - `.../F.cs:13778`
  - `.../F.cs:13800`

### 5.4 Relation influence

- Claim: private chat context includes actor-relative intimacy data and other cross-unit descriptors.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../i.cs:12138`
  - `.../A.cs:13911`

### 5.5 Sidecar persistence

- Claim: there is a per-message `JObject` sidecar store keyed by the private-message index.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../F.cs:13497-13517`

## 6. Group Chat Chain

### 6.1 Persisted group history is first-class

- Claim: group history is paged and replayed from persisted `GroupMessageData` records.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../F.cs:11913-11959`

### 6.2 Group chat uses per-line attribution

- Claim: the rendered UI reads `fromUnit`, `message.content`, date, point, and weather from each stored record.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../F.cs:11925-11959`

### 6.3 Speaking-order prompt exists

- Claim: the prompt set includes a dedicated speaking-order guide for group chat.
- Status: `supported by asset`
- Confidence: `high`
- Evidence:
  - `decoded_prompts/群聊_发言顺序引导.md`

### 6.4 Two-stage order-planning path

- Claim: both ad hoc group chat and contact-group chat use a separate planning request that returns an `npcs` array and then resolve those names to `WorldUnitBase` instances before per-speaker generation.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../l.cs:11175`
  - `.../l.cs:10870`
  - `.../Y.cs:12483`
  - `.../Y.cs:12209`
  - `decoded_prompts/群聊_发言顺序引导.md`
  - `decoded_prompts/传音群聊_发言顺序引导.md`

### 6.5 Per-speaker generation and effect application

- Claim: after order planning, the runtime generates one NPC reply at a time and applies parsed structured effects after each reply.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../J.cs:10913`
  - `.../J.cs:10918`
  - `.../l.cs:9094`
  - `.../l.cs:9108`
  - `.../l.cs:9112`
  - `.../i.cs:11087`

### 6.6 Message mirroring

- Claim: stored group/contact-group messages are mirrored into each participant's private-message history as synthetic `PrivateMessageData`.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../l.cs:11059`
  - `.../Y.cs:12368`

## 7. Long Memory Chain

### 7.1 Summary memory object

- Claim: long memory uses `ExperienceData` with a month bucket and summary content.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `.../OpenAIWorld.mod/ExperienceData.cs:3-7`

### 7.2 Monthly summary UI

- Claim: the memory UI computes labels using private-message count and host log counts per month.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../s.cs:11527-11556`

### 7.3 Import/export pipeline

- Claim: long-memory summaries can be serialized to disk and imported back as `List<ExperienceData>`.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../s.cs:11687-11723`
  - `.../s.cs:12025-12043`

### 7.4 Compression strategy

- Claim: memory compression is based on summarization rather than retrieval.
- Status: `supported by asset`
- Confidence: `high`
- Evidence:
  - `decoded_prompts/记忆压缩.md`

### 7.5 Compression pass exists in code

- Claim: the long-memory summary is produced by an LLM compression pass over one month's private messages and logs, then stored as `ExperienceData`.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../g.cs:10720`
  - `.../g.cs:10738`
  - `.../g.cs:10762`
  - `.../g.cs:10803`

## 8. Propagation Chain

### 8.1 Propagation as explicit command

- Claim: propagation is modeled as an explicit `ConveyMessage` action command.
- Status: `supported by asset`
- Confidence: `high`
- Evidence:
  - `decoded_prompts/信息裂变.md`

### 8.2 Remote-contact groups are distinct host objects

- Claim: remote group communication is structurally represented as `ContactGroup`.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `.../OpenAIWorld.mod/ContactGroup.cs:5-15`

### 8.3 Propagation becomes persisted direct message traffic

- Claim: accepted direct propagation is routed into persisted receiver-visible message traffic rather than by mutating `WorldEventData`.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `.../i.cs:11376-11431`
  - `.../F.cs:13614-13698`
  - `.../Y.cs:12999-13077`

Clarification:

- this upgraded claim is only about the readable direct-delivery path
- it confirms `ConveyMessage` can create a new private or remote direct communication event
- it does not prove that every possible future rebuild carrier must be source-identical

### 8.4 Direct delivery routing logic

- Claim: the readable recovered router for direct propagation chooses between local private dialogue and remote direct communication based on spatial reachability.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `.../i.cs:11396-11414`
  - `.../i.cs:11416-11431`

Boundary:

- what remains unresolved is not the existence of this direct branch
- what remains unresolved is whether any additional original branch paths also routed into contact-group or other indirect carrier surfaces outside this readable handler

## 9. Active World Chain

### 9.1 Durable world-event object

- Claim: world events are stored as durable objects with `id`, `eventType`, `roundTotalDay`, `content`, `locationName`, `soleID`, `point`, `icon`, `items`, `units`, and `triggered`.
- Status: `confirmed by code`
- Confidence: `high`
- Evidence:
  - `.../OpenAIWorld.mod/WorldEventData.cs:5-27`

### 9.2 Active-world scheduler exists

- Claim: active-world generation is scheduled and gated by auto-run settings.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../O.cs:12899`
  - `.../O.cs:13204`
  - `.../O.cs:13215`

### 9.3 Event persistence and update paths

- Claim: world events are persisted and updated through dedicated storage paths and looked up by `soleID`.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../O.cs:13238`
  - `.../O.cs:13244`
  - `.../O.cs:13693`

### 9.4 Event materialization

- Claim: parsed world-event output can create missing NPCs, bind existing units, and resolve items.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../O.cs:13569`
  - `.../O.cs:13585`
  - `.../O.cs:13616`
  - `.../O.cs:13624`
  - `.../O.cs:13663`
  - `.../O.cs:13112`

### 9.5 Triggered lifecycle bit

- Claim: `WorldEventData.triggered` is set when the related map event is opened/interacted with, then written back.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../E.cs:11707-11718`

### 9.6 World-event feedback loops

- Claim: previous world events are rendered for players and also fed back into later world-generation context.
- Status: `confirmed by code`
- Confidence: `medium`
- Evidence:
  - `.../c.cs:10484`
  - `.../i.cs:11000`
  - `.../O.cs:13481`

### 9.7 World prompt pair

- Claim: active world uses separate world-rules and world-event prompts.
- Status: `supported by asset`
- Confidence: `high`
- Evidence:
  - `decoded_prompts/世界定义.md`
  - `decoded_prompts/世界推演.md`

## 10. Main Reproduction Takeaways

- `high` confidence:
  - the system is channelized
  - the system is JSON-first
  - the system persists raw history, long-memory summaries, and world events separately
  - the system validates model output before host mutation

- `medium` confidence:
  - private and group action pipelines share a common parsed-action shape
  - world-event lifecycle is tied into map-event and world hooks

- `medium` confidence but reconstruction-only:
  - long-memory summaries can be reinjected into future generation contexts if a port chooses that design

- `low` confidence:
  - exact class ownership of every obfuscated subsystem
  - exact prompt override precedence
  - exact propagation routing chooser
