# Test Spec: Stardew Autonomy Intent Contract Tool Boundary

## Unit / Integration Tests

- `NpcAutonomyLoopTests.RunOneTickAsync_WithSpeechAndTaskUpdateIntent_SubmitsSpeechAndUpdatesExistingTodo`
  - parent returns JSON intent with `wait`, `speech`, and `taskUpdate`
  - local executor handles the wait
  - host submits a speak action
  - existing todo is updated to `blocked`
  - no extra todo is created

- `NpcAutonomyLoopTests.RunOneTickAsync_WithUnknownTaskUpdate_DoesNotCreateTodo`
  - parent returns `taskUpdate.taskId` that does not exist
  - host logs skip/diagnostic
  - todo snapshot remains unchanged

- `NpcRuntimeSupervisorTests.GetOrCreateAutonomyHandleAsync_ParentUsesContractOnlyWithoutRegisteredTools`
  - game tools, MCP tools, and local executor tools are provided
  - autonomy parent call uses no tool definitions
  - private chat tool behavior remains covered by existing private chat tests

- `NpcRuntimeContextFactoryTests.Create_AutonomyChannel_OmitsGlobalSkillsMandatoryIndex`
  - autonomy system prompt describes JSON-contract-only behavior
  - autonomy system prompt does not tell the parent to use registered tools
  - interactive/default context still keeps the normal registered-tool prompt

- Update existing tests that asserted `stardew_speak` remains on the autonomy parent surface. It should now be represented by `speech` in the intent contract.

## Live Verification

- Run the focused test filters for `NpcAutonomyLoopTests` and `NpcRuntimeSupervisorTests`.
- Build or run the main desktop test project if focused tests pass.
- Use the configured local OpenAI-compatible Qwen delegation model to run a real contract/local-executor tick.
- Manual Stardew check:
  - start desktop and game
  - watch `runtime.jsonl` for `parent_tool_surface verified registered_tools=0`
  - confirm no `max_tool_iterations`
  - confirm movement/speech/task updates happen through host/local executor logs
