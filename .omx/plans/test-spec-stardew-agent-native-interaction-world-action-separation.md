# Test Spec: Stardew Agent-Native Interaction / World Action Separation

## Scope

Verify that private-chat lifecycle terminal records are visible as interaction facts, not as real world action facts.

## Tests

### 1. Autonomy prompt filters interaction lifecycle terminal

File: `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`

Scenario:
- `LastTerminalCommandStatus` is `Action=open_private_chat`, `Status=completed`, command id starts with `work_private_chat:`.
- There is an active todo.

Expected:
- Agent decision message does not contain `last_action_result`.
- Agent decision message contains `interaction_session` or equivalent interaction fact.
- Agent decision message does not contain `active todo continuity`.

### 2. Recent activity classifies interaction terminal separately

File: `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`

Scenario:
- Runtime driver has `LastTerminalCommandStatus` with `Action=open_private_chat`, `Status=completed`.

Expected:
- Recent facts contain `lastInteraction=open_private_chat:completed:none`.
- Recent facts do not contain `lastAction=open_private_chat`.

### 3. Existing world action behavior remains

Existing tests remain valid:

- completed `move` still creates `last_action_result`.
- completed `move` plus active todo still creates active todo continuity fact.
- recent activity for real actions can still emit `lastAction=`.

### 4. Controller does not put interaction lifecycle into action-chain

File: `Desktop/HermesDesktop.Tests/Stardew/StardewNpcToolFactoryTests.cs`

Scenario:
- Submit `GameActionType.OpenPrivateChat` through `StardewRuntimeActionController.TryBeginAsync`.
- Record terminal completed submit result.

Expected:
- `LastTerminalCommandStatus` is still recorded as `open_private_chat`.
- `ActionChainGuard` remains null or unchanged.
- No `open_private_chat` action-chain/action-loop fact can be emitted from this controller path.

## Verification

Focused:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~NpcAutonomyLoopTests|FullyQualifiedName~StardewNpcToolFactoryTests" -p:UseSharedCompilation=false
```

Build:

```powershell
dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64 -p:UseSharedCompilation=false
```
