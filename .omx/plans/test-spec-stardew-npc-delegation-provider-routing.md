# Test Spec: Stardew NPC Delegation Provider Routing

## Unit Tests

1. `ChatRouteResolverTests`
   - Root config only returns root `LlmConfig`.
   - `stardew_autonomy.model` overrides only model while inheriting base URL/auth fields.
   - `delegation.base_url` overrides inherited root base URL.
   - Missing lane section falls back to root.
   - Empty lane values are ignored.

2. `AgentToolRoutingTests`
   - `AgentTool` constructed with a delegation client calls that client, not parent client.
   - Agent tool schema remains stable enough for existing `AgentToolSchemaTests`.
   - v1 flat mode does not expose recursive `agent` tool to child definitions unless explicitly enabled in a future option.

3. `OpenAiClientStreamingPayloadTests`
   - Structured streaming payload contains a system message or provider-equivalent system field when `systemPrompt` is provided.
   - Structured streaming payload includes `tools` and `tool_choice=auto` when tool definitions are provided.
   - Existing non-tool streaming still omits tools.

4. `NpcRuntimeSupervisorTests`
   - Private chat handle and autonomy handle can receive different lane clients.
   - `AgentCapabilityAssembler` registers `AgentTool` with delegation client when present.

5. Stardew orchestration tests
   - `StardewNpcAutonomyBackgroundServiceTests`: autonomy request logs/uses autonomy lane.
   - `StardewNpcPrivateChatAgentRunnerTests` or related orchestrator tests: private chat uses private-chat lane.

## Integration/Build

Run:

```powershell
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
```

## Manual Smoke

1. Configure `%LOCALAPPDATA%\hermes\config.yaml` with:

```yaml
stardew_autonomy:
  provider: openai
  base_url: http://127.0.0.1:1234/v1
  model: <local-model>
  api_key: lm-studio

delegation:
  provider: openai
  base_url: http://127.0.0.1:1234/v1
  model: <local-model>
  api_key: lm-studio
```

2. Run `.\run-desktop.ps1`.
3. Trigger autonomy, private chat, and an `agent` tool delegation.
4. Inspect `%LOCALAPPDATA%\hermes\hermes-cs\logs\hermes.log` for lane/provider/model evidence.

## Observability Acceptance

Logs must include enough structured fields to answer:

- Which lane was used?
- Which provider/model was selected?
- Was base URL inherited or overridden?
- Did a child `agent` use delegation lane?
- Was concurrency config only reserved?
