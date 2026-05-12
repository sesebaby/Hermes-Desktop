# Test Spec: Stardew Autonomy Tool Closure Harness Reference Alignment

## Target Tests

Primary file:
- `Desktop/HermesDesktop.Tests/Runtime/NpcAutonomyLoopTests.cs`

## Required Regression

`RunOneTickAsync_WithTerminalActionActiveTodoAndJsonToolText_RunsAutonomySelfCheckOnce`

Arrange:
- NPC runtime instance with active todo.
- Last terminal command is completed `move`.
- First fake agent response is plain JSON text `{"tool":"stardew_status","parameters":{}}`.
- Second fake agent response is an explicit `no-action:` reason.

Assert:
- Agent is called twice.
- Second prompt contains the self-check facts.
- No command was submitted.
- Runtime log records `task_continuity_no_action`.
- Runtime log does not record `task_continuity_unresolved`.

## Guardrails

- Existing tests that ensure free text / parent JSON do not route to hidden local executor must remain green.
- Existing private-chat lifecycle terminal tests must remain green.
- The self-check should not run when no active todo exists or when the first response already contains a real tool/todo call.

## Verification

Run:

```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Runtime.NpcAutonomyLoopTests" -p:UseSharedCompilation=false
```

Then run a broader Stardew/runtime slice and desktop build.

