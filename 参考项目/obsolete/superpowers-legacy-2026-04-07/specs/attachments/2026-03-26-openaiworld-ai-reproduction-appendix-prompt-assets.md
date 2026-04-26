# Appendix E: Prompt Asset Preservation And Comparison Baseline

## 1. Purpose

The decoded prompt corpus is a primary design asset of this mod.

Do not reduce it to "some example prompts."

For faithful cross-host reproduction:

- preserve the original decoded prompt files unchanged as a comparison baseline
- build normalized cross-host prompt bundles beside that baseline, not instead of it
- keep every rebuilt prompt key traceable back to one or more original files

The original comparison baseline in this repository is:

- `recovered_mod/GGBH_OpenAIWorld_20260326/decoded_prompts/`

## 1.1 Current-Architecture Interpretation Note

This appendix preserves prompt-asset lineage from the recovered original mod.

Descriptions such as `local multi-speaker turn generation` or `local group speaker-order planning` in the tables below describe the recovered original behavior, not a mandatory current deployment location.

Under the current framework direction:

- keep prompt lineage, prompt splitting, and step boundaries faithful to the recovered source
- but treat the rebuilt normalized prompt bundles and orchestration rule chain as `hosted-only` by default unless an approved contract explicitly classifies a fragment as `local-distributable`

## 2. Non-Negotiable Preservation Contract

When porting this system to another game:

1. copy the entire decoded prompt corpus into a read-only `original/` archive folder
2. preserve original filenames verbatim
3. preserve a SHA-256 manifest for every file
4. never edit the archived originals in place
5. place all host-specific rewrites into normalized bundle layers or overlays
6. keep a mapping table from normalized prompt keys back to the original comparison files

This is the only safe way to let later reviewers compare:

- what the recovered mod actually asked for
- what the rebuilt host-specific prompt now asks for

## 3. Full Preserved Prompt Corpus Manifest

All files below should remain preserved as the comparison corpus.

| Original File | Bytes | Lines | SHA-256 | Suggested Bucket |
| --- | ---: | ---: | --- | --- |
| `常规.md` | 451 | 11 | `AE680EA1831AC91E7EEF1C9CE019B40CCFF02402BDB7117E712E4A4BE3F2F0C2` | utility |
| `常规_带选项.md` | 819 | 26 | `97DDEC4160E0BB843515B02F22EE5A2C9B222C3145ED5EE22EED2236150B6AA5` | utility |
| `传音符.md` | 885 | 58 | `1C49DEC4E65FE456B47AE28C60612D5B1CC9933DC358E9A26271EF38EDAB8283` | remote-item |
| `传音群聊.md` | 4353 | 229 | `A824110FA535713E5473CBB76D3BE86ECE6E482A90344F3BEFB5F6E26E514C89` | core-remote-chat |
| `传音群聊_发言顺序引导.md` | 2589 | 116 | `497AE2B92B71E87A82029E898399ACD0DC724EB23997FD4632462DD75E0AA314` | core-remote-chat |
| `道具使用.md` | 1149 | 88 | `377F9CB7494E79BC5EE01EF0D8B4D1A0EC29F9CAC38AD91747FC6506A5B43B11` | action-extension |
| `对话.md` | 2892 | 94 | `6BBDBE3CFF999D267B603FEAC026F03C30EC1D57FFF438237A2A2AE69D63A341` | core-dialogue |
| `对话_场景_邀请参加竞技场.md` | 1097 | 36 | `40A56C99CAD4A1123BACDE411AD34AAAB4833099E27DDEB9D6B3D4F219AA773D` | scenario-extension |
| `发送传音符.md` | 863 | 50 | `3702DA5BE580565FCBEAD314E18C1269C26773F57068B439820002A210F78B2F` | remote-item |
| `记忆压缩.md` | 4044 | 251 | `54498DCB082C6692283F83A3EC6009FBAEE1C9716BA86AF474AA30F9E3396A91` | core-memory |
| `交易.md` | 2127 | 92 | `2E970CAAD3737807ED0DE7E9885B14D5D5F640D5489796B304728FE88820AF68` | action-extension |
| `角色卡模板.md` | 2705 | 81 | `E5DCCC79F9F7C7B1032D272963C81930E6EC7238B4F489B3493ECBDC14A5D375` | support-template |
| `解签大师.md` | 2819 | 99 | `923CF0003542EAC41C487E1B91C473A53F477DA3DF5FF62F92FBF8802CA73044` | scenario-extension |
| `竞技场_匹配.md` | 3538 | 130 | `7780BEC6EBBC21203B02C4E12E496829FAC87327527E23DE4DCC92DA73255CD7` | scenario-extension |
| `竞技场_匹配_单人.md` | 2968 | 148 | `D89A9F59943DDAE7049665421EB2DE96581C2EBFA9C0A01BD0F9B96FD92FD4FE` | scenario-extension |
| `竞技场_赛前下注.md` | 3686 | 203 | `A5312D66F891F60C268382A2CF6E589028F571DEADA199567372FA13553668A8` | scenario-extension |
| `竞技场_战前对话.md` | 838 | 41 | `680F9C1E9407ACC840CB2F1F9BEAC5A68FA2E11BA083DEF7C880F7D7402F2018` | scenario-extension |
| `竞技场赛后评价系统.md` | 2791 | 160 | `6B67524D0F0025240AA8AB0477CAFD5EA99415BB2E9FD89EA8FA934458CA81DF` | scenario-extension |
| `群聊.md` | 4039 | 201 | `FF78106DC70581949FAFF2AC133CC85978F9BBAE887C5B5C69D3A06FCE7D3C2B` | core-group-chat |
| `群聊_发言顺序引导.md` | 1848 | 77 | `36D1EC00CAA51DB9061026A8150A47A7C30862B4C44A554B58E1B0B87C704226` | core-group-chat |
| `身体交互.md` | 1443 | 114 | `A9F0B19A8E073B372543472EE74B5F9A4F6AE974BD68E3F298CD4598218F808C` | action-extension |
| `世界定义.md` | 3696 | 139 | `4696F5B25AB3D9F780048928FB5633647E95A9E5DEB6300CF1FBCC18AB6EB089` | core-world |
| `世界推演.md` | 6534 | 282 | `0150EB3E6422FD61BABD9980EC0835E746F9B014B95A2C3A0FC76CE8D15D5C71` | core-world |
| `双修场景.md` | 1080 | 78 | `B554BAEAFA3B0879BB8BEFBE8FECB5F63E59BE8FF3AA30D72C2DB36E8F69664B` | scenario-extension |
| `信息裂变.md` | 3600 | 160 | `822821463CFE13884DF092E17F82030B63F3761ACCBE384A4646D7BD285DBCD4` | core-propagation |
| `行为指令.md` | 13842 | 703 | `DE860CB7B8A47BE35A9A55B7ED3E52F9B0810E1722FAB67A56508D582E16144B` | core-actions |
| `主动对话.md` | 1421 | 98 | `98B042FA12DE82A2EE2775F9152CC10274CDAD6E1478C3791B7415C44260FBDE` | dialogue-extension |
| `自定义物品生成_储物法器.md` | 8278 | 265 | `2773CCCA199A54E76F84D9CC40AD92B04DA25D2ABD842ACF1666B03C346A87BD` | content-generation |
| `自定义物品生成_可食用类.md` | 6916 | 235 | `D8234AFD3D008A40280569DBED7BD8A096F020BC7164335CD020D655247BB2E0` | content-generation |
| `自定义物品生成_可御器类.md` | 8024 | 260 | `8262E0D5EDA4B983F65124171D638A45AB1089DF1208666A1557C6DE7A969FF6` | content-generation |
| `自定义状态生成.md` | 8049 | 268 | `8900EFCE8218F399A24FF0DB71473F620F89BF1741C1BA2B07BD442E33636F28` | content-generation |

## 4. Core Prompt Comparison Set

The files below are the core comparison set for reproducing the NPC/world AI architecture itself.

### 4.1 Canonical Mapping Table

| Canonical Prompt Key | Original Comparison File(s) | Role In Rebuild |
| --- | --- | --- |
| `world.rules` | `世界定义.md` | global world axioms and simulation tone |
| `world.event_generation` | `世界推演.md` | active-world event proposal generation |
| `channel.private_dialogue` | `对话.md` | one-to-one dialogue generation |
| `channel.group_chat` | `群聊.md` | local multi-speaker turn generation |
| `group.speaker_selection` | `群聊_发言顺序引导.md` | local group speaker-order planning |
| `channel.remote_routed_communication` | `传音群聊.md` | remote routed multi-speaker communication only |
| `group.remote_speaker_selection` | `传音群聊_发言顺序引导.md` | remote speaker-order planning |
| `protocol.propagation` | `信息裂变.md` | explicit cross-actor information forwarding |
| `protocol.actions` | `行为指令.md` | structured action vocabulary and output shape |
| `protocol.memory_compression` | `记忆压缩.md` | month-bucketed summary compression logic |

Optional extension key:

| Optional Prompt Key | Original Comparison File(s) | Role In Rebuild |
| --- | --- | --- |
| `channel.proactive_dialogue` | `主动对话.md` | optional extension for NPC-initiated openings |

### 4.2 Comparison Baseline Records

These records preserve the original file identity while telling the rebuilder what to compare against.

| Key | Original File | SHA-256 | What Must Be Preserved |
| --- | --- | --- | --- |
| `channel.private_dialogue` | `对话.md` | `6BBDBE3CFF999D267B603FEAC026F03C30EC1D57FFF438237A2A2AE69D63A341` | role framing, structured output expectation, NPC-perspective speaking |
| `channel.group_chat` | `群聊.md` | `FF78106DC70581949FAFF2AC133CC85978F9BBAE887C5B5C69D3A06FCE7D3C2B` | multi-party context packing, per-speaker naturalness, memory-sensitive replies |
| `group.speaker_selection` | `群聊_发言顺序引导.md` | `36D1EC00CAA51DB9061026A8150A47A7C30862B4C44A554B58E1B0B87C704226` | separate order-planning step before turn generation |
| `channel.remote_routed_communication` | `传音群聊.md` | `A824110FA535713E5473CBB76D3BE86ECE6E482A90344F3BEFB5F6E26E514C89` | remote-carrier semantics distinct from local proximity chat |
| `group.remote_speaker_selection` | `传音群聊_发言顺序引导.md` | `497AE2B92B71E87A82029E898399ACD0DC724EB23997FD4632462DD75E0AA314` | remote routed speaker planning remains a separate phase |
| `protocol.propagation` | `信息裂变.md` | `822821463CFE13884DF092E17F82030B63F3761ACCBE384A4646D7BD285DBCD4` | explicit `ConveyMessage` semantics instead of free-form rumor text |
| `protocol.actions` | `行为指令.md` | `DE860CB7B8A47BE35A9A55B7ED3E52F9B0810E1722FAB67A56508D582E16144B` | action-first structured output discipline and typed action concepts |
| `protocol.memory_compression` | `记忆压缩.md` | `54498DCB082C6692283F83A3EC6009FBAEE1C9716BA86AF474AA30F9E3396A91` | summary compression, actor ownership, key-fact retention, token economy |
| `world.rules` | `世界定义.md` | `4696F5B25AB3D9F780048928FB5633647E95A9E5DEB6300CF1FBCC18AB6EB089` | top-level world axioms and systemic pressure framing |
| `world.event_generation` | `世界推演.md` | `0150EB3E6422FD61BABD9980EC0835E746F9B014B95A2C3A0FC76CE8D15D5C71` | structured world-event generation with continuity against prior events |

Optional extension baseline:

| Key | Original File | SHA-256 | What Must Be Preserved |
| --- | --- | --- | --- |
| `channel.proactive_dialogue` | `主动对话.md` | `98B042FA12DE82A2EE2775F9152CC10274CDAD6E1478C3791B7415C44260FBDE` | NPC-initiated opening logic as an optional extension, not as a core required channel |

## 5. What To Keep Verbatim Vs What To Rewrite

### 5.1 Preserve As System Invariants

These patterns are part of the design value and should survive any port:

- prompts are role-bound, not generic assistant chat
- channel prompts are separated by communication mode
- speaker-order planning is separated from per-speaker generation
- action and propagation are explicit protocols, not hidden inside prose
- memory compression is a first-class prompt, not an accidental summarization side effect
- world-event generation is structured and continuity-aware

### 5.2 Rewrite Into Host-Native Semantics

These parts should usually be rewritten for Stardew Valley, RimWorld, or 太吾绘卷:

- xianxia-specific setting nouns
- cultivation-specific power language
- source-game item and status taxonomy
- source-game remote carrier fiction such as `传音阵盘`
- source-game content-generation branches that do not exist in the target host

### 5.3 Preserve As Comparison Comments In Normalized Files

For every normalized rebuilt prompt, keep a short header like:

```md
Original comparison file: `decoded_prompts/群聊.md`
Original SHA-256: `FF78106DC70581949FAFF2AC133CC85978F9BBAE887C5B5C69D3A06FCE7D3C2B`
Port note: xianxia-specific scene language replaced with host-native scene semantics.
```

This gives future reviewers a stable diff target.

## 6. Recommended Rebuild Layout

Do not store only the rewritten prompt set.

Use a layout like:

```text
prompt_assets/
  original/
    decoded_prompts/...
    manifest.sha256
  normalized/
    world.rules.md
    world.event_generation.md
    channel.private_dialogue.md
    channel.group_chat.md
    channel.remote_routed_communication.md
    group.speaker_selection.md
    group.remote_speaker_selection.md
    protocol.actions.md
    protocol.propagation.md
    protocol.memory_compression.md
    optional/channel.proactive_dialogue.md
  overlays/
    host/<game>/*.md
    fiction/<setting>/*.md
    local/*.md
```

## 7. Practical Comparison Workflow

When changing a prompt in the rebuilt system:

1. identify the canonical prompt key
2. open the original comparison file
3. compare role, task, output shape, and behavioral rules
4. preserve the invariant behavior
5. rewrite only the host-specific fiction and terminology
6. update the normalized prompt hash and change note

If a porter cannot explain which original prompt a rebuilt prompt came from, the prompt asset work is not complete.

## 8. Core Prompt Dossiers

This section turns the comparison baseline into a porting guide.

Each dossier answers:

- what the original prompt is doing
- what behavior is too valuable to lose
- what should be rewritten for a new host
- what failure modes to watch for during migration

### 8.1 `channel.private_dialogue` from `对话.md`

Original role:

- one-speaker response generator for direct dialogue
- strong NPC-perspective constraint
- structured JSON output with `content`

Recovered behavioral core:

- the model must speak only as `{{ASSISTANT.NAME}}`
- the model must not control the player or narrate omnisciently
- the content is internally segmented into semantic bands such as thought, expression, action, environment, spoken line, and important information
- the output is strict JSON, not prose around JSON

Do not lose:

- actor-perspective lock
- anti-god-mode rule
- output-only-JSON discipline
- separation between content categories inside the rendered text

Safe rewrite targets:

- xianxia identity markers
- named dialogue types
- color-category names if the target UI uses different render channels
- source-game special interaction categories such as `双修`

Unsafe rewrites:

- removing the "cannot control the other participant" rule
- flattening the output into one plain sentence with no semantic structure
- turning it into a generic assistant answer prompt

Target-host rewrite note:

- Stardew can remap semantic bands into portrait mood, dialogue, and event hint layers
- RimWorld may reduce decorative environment lines and emphasize state, intent, and social-log-relevant content
- 太吾绘卷 can keep stronger inner-thought and stance signaling, but still should not preserve xianxia-specific nouns blindly

### 8.2 `channel.group_chat` from `群聊.md`

Original role:

- one-speaker reply generator inside a multi-speaker room
- consumes personal memory, participants, scene, and prior group history

Recovered behavioral core:

- one NPC speaks at a time
- the speaker may react briefly, observe, interrupt, or stay sparse
- relation state affects tone
- the model must not speak for other participants
- recent room context matters more than a generic chat style

Do not lose:

- per-speaker isolation
- variable turn length
- relation-sensitive tone shifting
- non-repetition rule against parroting previous lines

Safe rewrite targets:

- social categories and relation labels
- scene fiction
- UI-facing color and annotation language

Unsafe rewrites:

- asking one model call to write the whole room
- forcing every selected speaker to produce long dramatic monologues
- removing the memory and participant conditioning

Target-host rewrite note:

- Stardew group chat should feel festival/saloon/cutscene-like
- RimWorld group chat should feel like colonists or factions exchanging terse, simulation-grounded remarks
- 太吾绘卷 can preserve denser social nuance, but still needs one-speaker-per-turn discipline

### 8.3 `group.speaker_selection` from `群聊_发言顺序引导.md`

Original role:

- separate scheduler prompt for local group speaking order
- outputs `npcs` JSON list rather than dialogue text

Recovered behavioral core:

- selection is separate from generation
- only NPCs may be returned
- no duplicates
- maximum count is explicitly capped at 6 in the recovered scheduler prompt
- priority is topic relevance, personality likelihood, and plot momentum

Do not lose:

- scheduler/generator separation
- structured order output
- duplicate exclusion
- explicit cap on chosen speakers

Safe rewrite targets:

- exact relevance language
- example scene vocabulary
- local social norms around interruption and pacing

Unsafe rewrites:

- collapsing scheduler into the speaker prompt
- letting the selector return player ids
- removing the hard cap and stable uniqueness rules

Target-host rewrite note:

- the cap and fallback policy should follow the master manual contract even if the host scene usually has fewer active speakers

### 8.4 `channel.remote_routed_communication` from `传音群聊.md`

Original role:

- one-speaker reply generator for remote group communication
- preserves remote-carrier constraints distinct from face-to-face chat

Recovered behavioral core:

- participants may be distributed across different places
- only voice/tone/signal-level perception is available
- no direct visual description of other participants' motions or surroundings
- pacing is more restrained than local overlap-heavy group chat

Boundary rule:

- this prompt is for remote multi-speaker communication rooms only
- one-to-one remote contact should derive from `channel.private_dialogue` plus a remote-carrier overlay
- propagation carriers such as one-off letters, rumors, or notifications belong under `protocol.propagation`, not this channel prompt

Do not lose:

- carrier-dependent perception limits
- remote-specific pacing differences
- one-speaker-per-turn structure

Safe rewrite targets:

- `传音阵盘` fiction
- signal vocabulary like `神识波动`
- host-native carrier name such as shared mail thread, comms channel, social discussion thread, or radio net

Unsafe rewrites:

- reusing the local group prompt unchanged
- allowing visual scene narration that the carrier cannot support
- forgetting that remote chat is still a distinct channel, not just a location-less group chat

Target-host rewrite note:

- in Stardew this may become a remote discussion thread, magical party-link, or another multi-speaker remote room surface
- in RimWorld it may become faction comms, radio-net conference, or another multi-speaker remote exchange surface
- in 太吾绘卷 it may become a sect-network conference or another multi-speaker remote discussion surface depending mod support

### 8.5 `group.remote_speaker_selection` from `传音群聊_发言顺序引导.md`

Original role:

- scheduler for remote group response order

Recovered behavioral core:

- still outputs a structured speaker list
- still excludes the player
- still caps speaker count at 6 in the recovered scheduler prompt
- remote carrier changes the pacing heuristic: fewer interruptions, more pauses, more selective reply order

Do not lose:

- separate remote scheduling logic
- pacing distinction from local room chat
- structured speaker-order output

Safe rewrite targets:

- the fiction of how the channel works
- the reasons why pauses or restrained replies occur

Unsafe rewrites:

- treating remote order planning as identical to local order planning
- returning a bare text explanation instead of machine-readable order

### 8.6 `protocol.propagation` from `信息裂变.md`

Original role:

- explicit protocol for forwarding information to actors outside the current exchange

Recovered behavioral core:

- `ConveyMessage` is not for the current local counterpart
- propagation is motive- and relation-dependent
- forwarded content may be delayed, selective, incomplete, emotional, or distorted
- propagation targets should be chosen by social relevance, not random reach

Do not lose:

- propagation as an explicit command, not implicit prose
- exclusion of current direct counterpart as propagation target
- selective forwarding logic
- allowance for biased or imperfect transmission

Safe rewrite targets:

- carrier names and examples
- social target classes
- fiction-specific help/request/intel examples

Unsafe rewrites:

- rewriting propagation as a hidden narrative side effect
- forcing every important fact to spread
- assuming propagation is always truthful and complete

Target-host rewrite note:

- keep the protocol semantic even if the target host has no literal "message sending" fiction
- if the host uses rumor boards, letters, notifications, or logs, the normalized rebuilt prompt should still express target choice, message content, and carrier preference

### 8.7 `protocol.actions` from `行为指令.md`

Original role:

- core structured action protocol for dialogue/event outputs
- defines `actions[]` shape and a large command vocabulary

Recovered behavioral core:

- action use depends on personality, emotion, relation, state, scene, and conversation content
- actions must be justified by conditions
- actions are explicit JSON objects
- the prompt contains many source-specific commands beyond the portable core

Do not lose:

- actions as first-class structured output
- condition-gated action reasoning
- strict machine-readable envelope
- clear distinction between text content and deterministic action requests

Safe rewrite targets:

- source-specific command table
- host-specific item/status/faction commands
- commands that only make sense in the xianxia ruleset

Unsafe rewrites:

- replacing `actions[]` with hidden prose intentions
- keeping source-specific command names that the host cannot implement
- letting the model invent arbitrary command signatures per game

Target-host rewrite note:

- use the master manual's canonical minimum action vocabulary as the stable bridge
- treat the original full action list as a source of ideas, not a table to copy blindly into Stardew or RimWorld

### 8.8 `protocol.memory_compression` from `记忆压缩.md`

Original role:

- compress chats, events, experiences, and older memories into dense actor-owned memory

Recovered behavioral core:

- optimize for token reduction without losing key relation and event facts
- memory ownership is actor-relative
- preserve commitments, conflicts, help, hostility, world intel, and major outcomes
- aggressively remove fluff and repetition

Do not lose:

- actor-relative ownership rule
- compression bias over exhaustive transcript retention
- explicit prioritization of relation change, promises, debt, intel, and consequential events

Safe rewrite targets:

- source-game nouns
- example content
- stylistic compression examples

Unsafe rewrites:

- converting compression into a neutral global summary of everyone
- losing the owner-relative perspective
- preserving too much fluff so the prompt stops functioning as compression

Target-host rewrite note:

- this prompt is one of the most transferable assets in the corpus
- its value is not xianxia flavor but disciplined memory distillation

### 8.9 `world.rules` from `世界定义.md`

Original role:

- global world axioms for the simulation

Recovered behavioral core:

- establishes the world's pressure system
- encodes what power, scarcity, time, and social structure mean
- gives the model a background physics for evaluating dialogue and events

Do not lose:

- global-rule layer separate from per-channel prompts
- explicit time/world progression framing
- explicit relation and power semantics

Safe rewrite targets:

- cultivation hierarchy
- resources, factions, geography, and cosmology
- host-specific progression ladders

Unsafe rewrites:

- removing the world-rule layer entirely
- hiding world logic only in examples
- mixing all world rules into every channel prompt ad hoc

Target-host rewrite note:

- Stardew's world rules should encode social rhythms, seasons, festivals, local reputation, town routines
- RimWorld's should encode colony survival pressure, factions, incidents, scarcity, biome danger
- 太吾绘卷's should encode sects, stances, jianghu consequence, social obligations, timeline continuity

### 8.10 `world.event_generation` from `世界推演.md`

Original role:

- structured generator for world events that continue prior history

Recovered behavioral core:

- events must not duplicate recent history
- events should advance continuity
- events should include conflict, opportunity, risk, and exploration value
- output is structured JSON with content, items, units, addUnits, location, and locationType

Do not lose:

- continuity against historical events
- structured event payload
- event composition around conflict/risk/opportunity rather than flavor-only narration

Safe rewrite targets:

- source-fiction location/item terminology
- reference-novel dependency
- content examples

Unsafe rewrites:

- converting event generation into a plain lore paragraph
- dropping structured participants/items/location fields
- generating disconnected one-off scenes with no continuity check

Target-host rewrite note:

- in Stardew this may generate town incidents, seasonal discoveries, visitor events, rumor-backed quests
- in RimWorld it may generate incident seeds, site rumors, encounter setups, faction pressure events
- in 太吾绘卷 it may generate jianghu incidents, sect movements, travel encounters, local crises

## 9. Cross-Host Rewrite Matrix

Use this matrix when rewriting the normalized prompt layer.

| Preserve Exactly In Behavior | Rewrite Into Host Semantics | Usually Delete |
| --- | --- | --- |
| JSON-only output rules | xianxia nouns and metaphysics | source-only erotic or niche side-scenario branches that the target host cannot support |
| one-speaker perspective lock | power ladder labels | UI color markup if the target renderer does not use inline color tags |
| action/propgation as explicit protocols | relation vocabulary | source-only content-generation branches with no host pipeline |
| scheduler-before-generation split | carrier fiction | reference-novel hooks that do not exist in the target port |
| actor-owned memory compression | world item taxonomy | duplicated examples once the invariant is captured elsewhere |
| structured world-event payload | location names and faction names | source-only minigame prompt branches if outside port scope |

## 10. Migration Acceptance Checks For Prompt Work

Prompt migration is not complete unless all checks pass:

- every normalized prompt key names its original comparison file and original SHA-256
- every normalized prompt states which invariants were intentionally preserved
- every host-specific overlay states what source semantics it replaced
- no rewritten prompt silently drops JSON-only output requirements where the source prompt required them
- no group or remote prompt allows one model call to speak for multiple actors
- no memory-compression rewrite loses owner-relative perspective
- no world-event rewrite loses structured payload fields

If any one of those checks fails, the prompt asset migration is incomplete.
