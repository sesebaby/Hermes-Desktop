# Stardew MCP Action Tools Context

## Task Statement

User asked to implement the previously discussed long-term architecture for Stardew NPC movement: expose real game actions through an MCP-aligned tool surface while preserving the async completion event -> NPC runtime -> agent wake-up loop.

## Desired Outcome

Move/action tools should be available through a true MCP JSON-RPC `tools/list` / `tools/call` surface, not only a custom HTTP endpoint. The implementation must keep the host/bridge as the only executor of real game writes, keep parent agent ownership of target selection, and keep completed/blocked/failed movement facts feeding back into NPC runtime so the agent can continue after arrival.

## Known Facts / Evidence

- Current branch is `allgameinai...origin/allgameinai [ahead 13]`.
- Previous commit `d5ecdb0c` already fixed completed move status being overwritten by `action_slot_timeout`, injected `last_action_result` into the next autonomy wake, and kept the full test suite green at that time.
- Official MCP transport guidance uses JSON-RPC messages over stdio or a single Streamable HTTP endpoint. `tools/list` discovers tools and `tools/call` invokes them with `{ name, arguments }`.
- Current `src/mcp/McpServer.cs` is named MCP but currently exposes custom HTTP endpoints:
  - `GET /mcp/tools/list`
  - `POST /mcp/tools/call`
  - `GET /mcp/info`
- Current `src/mcp/McpServerConnection.cs` and `src/mcp/McpToolWrapper.cs` already consume external MCP tools via JSON-RPC `tools/list` and `tools/call`.
- Current Stardew tools are Hermes-native `ITool` implementations in `src/games/stardew/StardewNpcTools.cs`.
- `StardewNpcToolFactory.CreateDefault(...)` exposes parent-safe Stardew tools, including `stardew_navigate_to_tile`, `stardew_speak`, `stardew_open_private_chat`, and `stardew_task_status`.
- `StardewNavigateToTileTool` submits a `GameAction` to `IGameCommandService`, polls status, and records terminal status through `StardewRuntimeActionController`.
- `NpcAutonomyLoop.BuildDecisionMessage(...)` already injects `last_action_result` from `LastTerminalCommandStatus` into the next wake-up prompt.

## Constraints

- No hardcoded NPC/place aliases or destination rules.
- Host/bridge executes reality; parent agent decides what to do.
- MCP does not replace the async completion loop; MCP is the invocation protocol, not the brain or event bus.
- Do not route internal NPC autonomy through loopback MCP if the in-process tool registry can provide the same transcript/tool result semantics with fewer lifecycle failures.
- Do not delete retained local executor/delegated ingress compatibility paths unless a separate plan explicitly replaces them.
- Keep changes small, test-first, and reversible.

## Unknowns / Open Questions

- Whether existing `McpServer` tests assume the custom REST endpoints and need compatibility retained during migration.
- Whether a full MCP initialize/session lifecycle exists on the server side or must be introduced minimally for `tools/list` / `tools/call` compatibility.
- Whether external MCP exposure should include only Stardew tools or all registered Hermes tools. Initial safe direction: implement general MCP server wrapping `ITool`, then choose tool set at composition time.

## Likely Codebase Touchpoints

- `src/mcp/McpServer.cs`
- `src/mcp/McpTypes.cs`
- `src/mcp/McpServerConnection.cs`
- `src/mcp/McpToolWrapper.cs`
- `src/games/stardew/StardewNpcTools.cs`
- `src/games/stardew/StardewNpcAutonomyBackgroundService.cs`
- `src/runtime/NpcAutonomyLoop.cs`
- `Desktop/HermesDesktop.Tests/Mcp/*`
- `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`
