# Test Spec: Hermes Desktop Python Skill Self-Evolution Parity

## Unit Tests
- `SkillManageToolSchema_UsesPythonActionsAndRequiredFields`
- `SkillManageTool_CreatePatchEditDeleteSkill`
- `SkillManageTool_WriteAndRemoveSupportingFileWithinAllowedDirs`
- `SkillManageTool_RejectsPathTraversalAndUnsupportedDirs`
- `SkillsListAndViewTools_ReturnSkillInventoryAndFullContent`
- `SystemPrompts_Build_IncludesSkillsGuidanceOnlyWhenRequested`
- `Agent_SkillReviewNudge_TriggersAfterConfiguredToolIterations`
- `Agent_SkillReviewNudge_ResetsWhenSkillManageIsUsed`
- `MemoryReviewService_CombinedReview_ExecutesMemoryAndSkillToolCalls`

## Regression Tests
- Existing `MemoryToolTests`, `MemoryReviewServiceTests`, `MemoryParityTests`, `TranscriptStoreTests`, and `AgentTests` remain green.

## Verification Commands
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore --filter "Skill|MemoryReviewServiceTests|AgentTests"`
- `dotnet test Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj --no-restore`
- `dotnet build HermesDesktop.slnx --no-restore`
