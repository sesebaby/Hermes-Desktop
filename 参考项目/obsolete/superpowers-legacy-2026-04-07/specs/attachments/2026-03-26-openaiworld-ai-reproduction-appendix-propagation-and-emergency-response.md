# Appendix E: Propagation And Emergency Response Clarification

> Current-architecture interpretation note:
>
> This appendix preserves recovered behavior evidence.
> Preserve the chain semantics it documents, but read deployment location through the active framework docs: hosted orchestration is allowed; local deterministic execution remains the host-truth boundary.

## 1. Purpose

This appendix exists to prevent one narrow, vivid example from becoming the whole interpretation of the propagation system.

The recovered `OpenAIWorld` source does support dramatic cases such as:

- an NPC is threatened
- the NPC asks a parent or other protector for help
- the target actor responds immediately

But that scene is only one instance of a broader mechanism.

`信息裂变` is not "the kin-rescue feature."

It is a general-purpose propagation protocol centered on `ConveyMessage`.

## 2. What Is Directly Confirmed

### 2.1 Propagation Is A First-Class Prompted Capability

Recovered prompt evidence:

- `decoded_prompts/信息裂变.md`

That prompt explicitly allows propagation for:

- help / rescue requests
- reporting secrets or intelligence
- warnings
- retaliation or threat notification
- dying words / final messages

It also states that recipients may be selected from relationship categories such as:

- friends / companions
- sect / family
- superiors / subordinates
- intelligence organizations
- hostile factions
- interested parties

So the source-accurate conclusion is:

- propagation is a general social-information mechanic
- kin rescue is a valid subset, not the defining case

### 2.2 Propagation Is Modeled As `ConveyMessage`

Recovered prompt and action evidence:

- `decoded_prompts/信息裂变.md`
- `decoded_prompts/行为指令.md`
- `.../F.cs:13827-13839`
- `.../i.cs:11062-11084`
- `.../i.cs:11087-11352`

The model emits structured JSON with `actions[]`.

`ConveyMessage` is one accepted action type.

### 2.3 The Readable Router For Direct Propagation Is Now Code-Anchored

Recovered code anchor:

- `.../i.cs:11376-11431`

What that handler directly proves:

1. it resolves one target actor by name
2. it resolves one message body
3. if sender and target share the same location, it routes into direct private chat
4. otherwise it routes into remote direct communication

This means the readable recovered source already anchors a direct one-target propagation router.

The most source-accurate summary is:

- `ConveyMessage` becomes a new real communication event
- for direct delivery, the source chooses between local private dialogue and remote direct communication based on spatial reachability

### 2.4 Propagation Creates Receiver-Visible Message State

Recovered code anchors:

- local private path: `.../F.cs:13614-13698`
- remote direct path: `.../Y.cs:12999-13077`

Both paths persist new `PrivateMessageData` records.

So propagation is not merely flavor narration.

It creates future context for the receiving actor.

## 3. Why The Parent-Rescue Example Works

The recovered source does not show a hardcoded rule like:

- "if threatened, call mother"

Instead the private-dialogue request builder injects:

- world rules
- channel rules
- action protocol
- optional propagation protocol
- full self snapshot
- relationship/context summary
- recent message history

Anchors:

- `.../F.cs:13721-13777`
- `.../i.cs:12155-12334`
- `.../i.cs:12138-12147`

Recovered host-side relation data also shows that kinship really exists as host state, not prompt fiction.

Relevant evidence includes family-tree and relation writeback paths such as:

- `.../R.cs:12352-12460`
- `.../A.cs:13277-13345`

So a parent-rescue scene is best explained as:

1. the model sees family/kin context as part of actor state
2. the propagation prompt explicitly allows asking family for help
3. the model chooses a parent as a plausible target
4. code resolves that named actor and starts a real communication turn

## 4. Why The Responder Can Appear Immediately

This must be separated from active-world event generation.

### 4.1 What Is Confirmed

Once the recipient receives the propagated message, the recipient immediately enters their own AI response path.

Direct anchor:

- `.../i.cs:11376-11431`

The callback after delivery immediately routes parsed actions back through the deterministic action executor.

So the strongest source-accurate explanation is:

- the responder receives a real message
- the responder generates their own structured reply and actions
- those actions are executed immediately in that same response chain

### 4.2 Movement-Like Arrival

Recovered action and execution anchors:

- `decoded_prompts/行为指令.md`
- `.../i.cs:11694-11703`
- `.../A.cs:13174-13192`

The recovered source shows that `GoTo` resolves into a host-native point-setting path, ultimately creating `UnitActionSetPoint(...)`.

So the most conservative interpretation is:

- the dramatic "arrived immediately" effect is best explained by a direct movement/set-point action in the responder chain
- not by the slower periodic active-world director path

### 4.3 What Is Still Only Inference

The following narrower claims are still not fully proven:

- that the exact action emitted in the demo clip was literally `GoTo`
- that no host-side presentation layer adds any visual transition around the set-point action

What is proven is narrower and sufficient:

- the responder enters an immediate direct-response chain
- movement-to-point style execution exists and is a better fit than active-world scheduling

## 5. What Must Not Be Misstated

Do not collapse these three things into one:

1. `ConveyMessage` as a general propagation protocol
2. kin/family rescue as one plausible use-case
3. remote contact-group chat as a distinct channel

The recovered source supports all three, but they are not the same subsystem.

The clean mental model is:

- `ConveyMessage`:
  - one actor decides to inform or request help from another out-of-scene actor
- direct local/private or remote one-to-one routing:
  - the readable recovered router for direct delivery
- `ContactGroup` / `ContactGroupMessageData`:
  - a separate remote multi-speaker room/thread surface
- active world:
  - a separate world-event generation subsystem

## 6. Reproduction Guidance

If rebuilding this behavior in another host game, reproduce it this way:

1. keep `CONVEY_MESSAGE` as a real structured action, not free-form rumor text
2. feed the model actor-relative relation context, kin/family context, and memory context
3. let the model choose the recipient
4. route direct delivery deterministically:
   - local direct talk if reachable
   - remote direct message if not
5. persist the delivered message as receiver-visible state
6. let the receiver run a real response turn with their own actions
7. treat kin rescue as one scenario supported by the general protocol, not as a dedicated hardcoded feature
