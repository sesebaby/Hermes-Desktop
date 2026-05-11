# Stardew MCP Action Tools Implementation Plan

## Requirements Summary

Implement the long-term Stardew action architecture: actions such as `stardew_navigate_to_tile` must be callable through an MCP-aligned tool surface while the real execution remains in the host/Stardew bridge and completed action facts continue flowing back into NPC runtime and the next agent wake-up.

This plan explicitly rejects replacing the runtime completion loop with MCP alone. MCP provides the call/return protocol; the host action controller, bridge status events, runtime state, and autonomy wake facts remain the continuity loop.

## RALPLAN-DR Summary

### Principles

- **Protocol honesty:** if code says MCP, it must speak MCP JSON-RPC methods such as `tools/list` and `tools/call`, not a custom REST lookalike.
- **Agent-native boundary:** the model chooses targets and calls tools; host/bridge only execute and report facts.
- **Single execution core:** MCP and native `ITool` surfaces must share the same Stardew action implementation, not drift into two behavior paths.
- **Completion continuity:** async terminal status must be persisted into NPC runtime and injected into later agent turns.
- **No hardcoded world semantics:** no place alias tables, no NPC-specific destination rules, no hidden host decisions after arrival.

### Decision Drivers

- The user needs reliable post-move continuation: arrival must become an agent-visible fact.
- The repo already has working in-process `ITool` tooling and external MCP client discovery.
- Current `McpServer` is not protocol-aligned enough to be the long-term answer.

### Viable Options

1. **Shared core + dual surface**
   - Keep internal NPC autonomy on native `ITool` registration.
   - Make `McpServer` expose those same `ITool` objects via MCP JSON-RPC `tools/list` / `tools/call`.
   - Pros: least lifecycle risk, no loopback networking inside the NPC runtime, same action core and same completion loop.
   - Cons: internal runtime is not literally consuming its own MCP server.

2. **Loopback MCP for NPC autonomy**
   - Start a local MCP server and have NPC runtime consume Stardew actions through `McpManager` as MCP-wrapped tools.
   - Pros: every action invocation path is literally MCP.
   - Cons: adds startup ordering, HTTP/session, auth, serialization, and failure modes without improving completion continuity.

3. **External MCP only**
   - Expose Stardew tools externally through MCP but leave all internal surfaces unchanged.
   - Pros: simplest.
   - Cons: does not address the user's desire that move/action semantics be represented as tool protocol, and risks two mental models.

Chosen option: **Option 1, shared core + dual surface**. It satisfies MCP protocol honesty without making in-process autonomy depend on loopback transport.

## Implementation Steps

1. **Lock MCP server protocol behavior with tests**
   - Add or update tests under `Desktop/HermesDesktop.Tests/Mcp/`.
   - Verify `POST /mcp` accepts JSON-RPC envelopes for `initialize`, `tools/list`, and `tools/call`.
   - Verify response `jsonrpc`, `id`, `result`, and `error` fields follow JSON-RPC shape.
   - Verify `notifications/initialized` is accepted as a no-response notification and unknown notifications are ignored or handled without crashing.
   - Verify `tools/list` returns schemas from `IToolSchemaProvider` when available.
   - Verify `tools/call` maps MCP arguments into the existing `ITool.ParametersType` and returns MCP content blocks plus `isError`.
   - Verify MCP text content uses `{ "type": "text", "text": "..." }`, not Hermes-internal `value`.
   - Keep legacy custom endpoints only if existing tests or callers rely on them; mark them compatibility, not the primary protocol.

2. **Upgrade `src/mcp/McpServer.cs` to MCP JSON-RPC**
   - Add a single Streamable-HTTP-compatible JSON endpoint, initially `POST /mcp`.
   - Implement `GET /mcp` as `405 Method Not Allowed` for now because this server does not provide standalone SSE streams yet.
   - Require/validate `Accept` on MCP POST requests when present; tests should cover accepting `application/json` and rejecting clearly unsupported content negotiation.
   - Accept valid JSON-RPC notifications with HTTP `202 Accepted` and no body.
   - Keep local security posture: bind only to `localhost`, keep bearer auth, and reject non-local `Origin` headers when an `Origin` header is supplied.
   - Target MCP protocol revision: prefer latest stable `2025-11-25`; keep `2025-06-18` as an explicitly tested compatibility version because existing docs/tests may already reference it.
   - For requests after initialization, validate `MCP-Protocol-Version` when supplied; accept negotiated/supported versions (`2025-11-25`, `2025-06-18`) and reject invalid/unsupported values with HTTP 400.
   - POST `Accept` validation must accept the standard Streamable HTTP header shape containing both `application/json` and `text/event-stream`, and may also accept plain `application/json` for local compatibility tests.
   - Parse JSON-RPC request envelopes.
   - Implement minimal server methods:
     - `initialize`
     - `tools/list`
     - `tools/call`
   - `initialize` must return a concrete contract:
     - `protocolVersion`: echo the requested version when it is one of the supported versions.
     - If the requested version is missing or unsupported, return the server's preferred version (`2025-11-25`) only when following MCP negotiation semantics for a well-formed client version mismatch; return JSON-RPC `-32602` for invalid, empty, or malformed protocol version values.
     - `capabilities.tools.listChanged`: `false` for this static registry.
     - `serverInfo.name`: `hermes-desktop`.
     - `serverInfo.version`: current server package/version fallback, initially `"1.0.0"`.
     - `serverInfo` may include only stable fields in this slice; no invented product metadata is required.
   - Return JSON-RPC errors with actionable messages for missing method, unknown tool, invalid params, and invalid arguments.
   - Use JSON-RPC protocol errors for malformed envelopes and unknown methods; use MCP `CallToolResult.isError=true` for normal tool execution failures.
   - Keep bearer auth behavior intact.

   Error classification table:

   | Condition | HTTP | JSON-RPC / MCP result |
   | --- | --- | --- |
   | Missing/invalid bearer token | 401 | plain JSON auth error for compatibility |
   | Non-local `Origin` on local server | 403 | plain JSON auth/security error |
   | Malformed JSON | 400 | JSON-RPC error without `id` when possible |
   | JSON-RPC envelope missing `jsonrpc`, `method`, or invalid request shape | 200 or 400 depending parseability | JSON-RPC `-32600` |
   | Unknown JSON-RPC method | 200 | JSON-RPC `-32601` |
   | Malformed/empty `initialize` protocol version or invalid `tools/call` params | 200 | JSON-RPC `-32602` |
   | Well-formed but unsupported `initialize` version | 200 | negotiate by returning preferred supported version (`2025-11-25`) with supported/requested detail where useful |
   | Unknown tool name | 200 | JSON-RPC `-32602` |
   | Tool executes and returns `ToolResult.Fail` | 200 | `result.isError=true`, text content contains tool failure |
   | Tool throws unexpectedly | 200 | `result.isError=true`, text content contains actionable execution failure |

3. **Fix shared MCP DTO compatibility**
   - Update `McpContentBlock.Text` serialization/deserialization so inbound and outbound MCP use `text`.
   - Keep wrapper formatting compatible with external MCP servers that return standard text blocks.
   - Add a small round-trip test for server output and `McpToolWrapper`/connection parsing if feasible.

4. **Define tool ownership and registry boundaries**
   - MCP server exposes the exact `ITool` registry it is constructed with; it does not discover or switch NPCs at call time.
   - For Stardew/NPC action tools, the registry must be built for one descriptor/runtime owner, so `stardew_navigate_to_tile` is descriptor-bound and cannot silently move another NPC.
   - Multi-NPC external MCP exposure is a later layer that must namespace tools or servers explicitly; do not add implicit current-NPC selection in this change.
   - This implementation scope delivers the protocol-correct generic `McpServer` adapter plus descriptor-bound Stardew MCP regression construction. It does not yet add an always-on product UI toggle or multi-NPC MCP server lifecycle.
   - If Desktop has an existing MCP-server startup path, wire the improved server there without changing startup semantics; otherwise do not invent a new always-on external listener in this slice.

5. **Reuse Stardew action tools unchanged as the MCP backing implementation**
   - Do not create a second `stardew_mcp_move` implementation.
   - `StardewNavigateToTileTool` remains the execution core: it submits `GameAction`, polls status, and records terminal status through `StardewRuntimeActionController`.
   - MCP exposure should call that same `ITool`, so completion state still reaches `NpcRuntimeDriver` and later `NpcAutonomyLoop`.

6. **Add MCP/Stardew regression tests**
   - Verify `stardew_navigate_to_tile` exposed via MCP calls `IGameCommandService.SubmitAsync` with the exact target fields.
   - Verify source/facing/thought payload fields pass through unchanged.
   - Verify successful command/status results are returned as tool result text and do not bypass `LastTerminalCommandStatus`.
   - Add a mandatory regression covering `tools/call -> StardewNavigateToTileTool -> RecordStatusAsync -> LastTerminalCommandStatus -> next wake last_action_result` with a descriptor-bound runtime driver.
   - Cover at least one successful terminal status and one failure/blocking terminal status in that completion-chain regression.
   - Verify no hardcoded destination aliases or destinationId path is introduced.

7. **Preserve NPC runtime completion wake-up behavior**
   - Keep `NpcAutonomyLoop.BuildLastActionResultFact(...)` behavior from the prior fix.
   - Keep or add a narrow test: a completed move status must produce `last_action_result` in the next agent message.
   - Do not have the host decide "what to do after arrival"; the fact only wakes the agent with reality.

8. **Verification**
   - Run targeted MCP/Stardew/runtime tests first.
   - Run the full desktop test project if targeted tests pass.
   - Inspect `git diff` for accidental hardcoded place/NPC rules and accidental deletion of completion loop code.

## Acceptance Criteria

- `McpServer` has a tested JSON-RPC `POST /mcp` path for `initialize`, `tools/list`, and `tools/call`.
- MCP JSON-RPC responses preserve request `id`; notifications do not produce ordinary response bodies.
- `initialize` returns negotiated protocol version, `capabilities.tools.listChanged=false`, and `serverInfo`.
- `initialize` prefers `2025-11-25`, echoes supported requested versions, and keeps a compatibility test for `2025-06-18`.
- Invalid/malformed protocol versions are rejected with JSON-RPC `-32602`; well-formed unsupported versions follow MCP negotiation by returning a supported version.
- `GET /mcp` returns `405` until SSE support is deliberately implemented.
- Standard POST `Accept: application/json, text/event-stream` is accepted.
- MCP POST notification requests return `202 Accepted` with no body.
- Local security checks keep bearer auth and reject non-local `Origin` headers when supplied.
- MCP `tools/list` includes actual tool schemas where tools implement `IToolSchemaProvider`.
- MCP `tools/call` returns standard MCP content blocks with `text`, not non-standard `value`.
- MCP `tools/call` invokes the same descriptor-bound `ITool` implementation used by the in-process runtime.
- `stardew_navigate_to_tile` works through MCP without a separate movement implementation.
- Terminal move status still records into `NpcRuntimeInstance.LastTerminalCommandStatus`.
- Next autonomy wake can see a completed/blocked/failed move through `last_action_result`.
- At least completed and one blocked/failed terminal action result are covered through the MCP call completion-chain test.
- No new hardcoded location/NPC routing rules are added.

## Risks And Mitigations

- **Risk:** existing callers depend on `/mcp/tools/list` and `/mcp/tools/call`.
  - Mitigation: keep compatibility endpoints while making `/mcp` the protocol-correct primary path.
- **Risk:** implementing too much MCP lifecycle creates scope creep.
  - Mitigation: implement minimal `initialize`, `tools/list`, `tools/call` only.
- **Risk:** loopback MCP would create race conditions in NPC startup.
  - Mitigation: keep NPC autonomy using in-process `ITool`; use MCP server as an external protocol adapter over the same core.
- **Risk:** MCP call returns immediate queued status while movement completes later.
  - Mitigation: preserve status polling and `LastTerminalCommandStatus`; the async completion loop remains authoritative for later wake-up facts.
- **Risk:** external MCP callers accidentally control the wrong NPC.
  - Mitigation: this change exposes only the registry supplied to `McpServer`; Stardew action registries are descriptor-bound, and multi-NPC namespacing is deferred.
- **Risk:** Hermes MCP client/server interoperate only with themselves.
  - Mitigation: test standard `text` content blocks and JSON-RPC envelopes against protocol-shaped JSON, not only internal DTO names.

## Verification Steps

- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~McpServer|FullyQualifiedName~StardewNpcToolFactory|FullyQualifiedName~NpcAutonomyLoop" -p:UseSharedCompilation=false`
- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug -p:UseSharedCompilation=false`

## ADR

### Decision

Implement MCP as a protocol adapter over the existing Hermes `ITool` action core, and keep NPC autonomy internally registered against the in-process tool registry.

### Drivers

- Avoid lying about MCP compatibility.
- Keep runtime completion facts reliable.
- Avoid loopback networking inside the NPC decision loop.

### Alternatives Considered

- **Route NPC autonomy through loopback MCP:** rejected because it adds lifecycle and transport failure modes while preserving neither more cognition nor better completion events.
- Steelman: it would make internal and external invocation protocol paths literally identical and improve protocol discoverability symmetry. Rejection remains because it adds transport/session/auth/startup failure modes to the NPC autonomy critical path without improving who decides or how completion facts wake the agent.
- **Expose external MCP only:** rejected because it leaves the long-term action surface ambiguous.
- **Create a separate Stardew MCP move implementation:** rejected because it would split action semantics and increase drift risk.

### Why Chosen

Shared core + dual surface gives agents a proper tool protocol externally and keeps the already-working in-process NPC runtime simple. MCP call results and async completion facts both originate from the same action implementation.

This is not claiming the internal NPC runtime is MCP-loopback-first. The deliberate architectural boundary is: **one execution core, two call surfaces**. The native in-process surface is the reliability path for NPC autonomy; the MCP surface is the standard external protocol adapter over the same core.

### Consequences

- MCP becomes a real external surface for Stardew/Hermes tools.
- Internal NPC autonomy can continue using direct `ITool` without violating the architecture, because the model still calls tools and the host still executes reality.
- Future external clients can call the same action tools through standard MCP.
- Multi-NPC external MCP exposure still needs an explicit namespacing/server ownership design before it becomes a product surface.

### Follow-ups

- Consider a later dedicated MCP server startup/config UI if external clients need stable discovery.
- Consider streaming/SSE support only after `tools/list` and `tools/call` are stable.
- Consider exposing resources for Stardew navigation knowledge later; do not put POI hardcoding into action tools.

## Available Agent Types Roster

- `explore`: quick repo lookup and symbol mapping.
- `architect`: architecture review and boundary validation.
- `critic`: plan consistency and risk review.
- `executor`: implementation.
- `test-engineer`: targeted test additions and regression coverage.
- `verifier`: final test and evidence audit.
- `code-reviewer`: review before commit if the diff grows broad.

## Follow-up Staffing Guidance

### `$ralph` Path

Use one `executor` as owner with `test-engineer` or `verifier` review after implementation. Suggested reasoning: executor medium, verifier high.

### `$team` Path

Use parallel lanes only if scope expands:

- MCP lane: `executor`, owns `src/mcp/*` and MCP tests.
- Stardew lane: `executor` or `test-engineer`, owns Stardew MCP regression tests.
- Verification lane: `verifier`, owns final targeted/full tests and hardcoding scan.

## Launch Hints

```powershell
$ralph .omx/plans/stardew-mcp-action-tools-20260511.md
```

```powershell
$team .omx/plans/stardew-mcp-action-tools-20260511.md
```

## Team Verification Path

Team must prove:

- MCP JSON-RPC tests pass.
- Stardew action-through-MCP regression passes.
- `NpcAutonomyLoop` completion fact tests pass.
- Full desktop tests pass or any failures are unrelated and documented.

## Applied Review Changelog

- Initial draft records the protocol risk explicitly and rejects loopback MCP as the default internal runtime path.
- Architect iteration applied: added JSON-RPC envelope/notification/error details, standard `text` content-block compatibility, descriptor-bound runtime ownership, completion-chain regression, and legacy endpoint lifecycle constraints.
