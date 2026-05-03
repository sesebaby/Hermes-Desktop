# 测试规格：星露谷 NPC 世界背景与地点候选

## 自动化测试
- `StardewQueryServiceTests.ObserveAsync_MapsPlaceCandidatesIntoMachineReadableFacts`
  - 构造 status payload 包含 4 个 `placeCandidates`。
  - 断言 observation facts 只输出前 3 个。
  - 断言 facts 包含 `label/locationName/x/y/tags/reason`。

- `StardewAutonomyTickDebugServiceTests.RunOneTickAsync_HaleyInjectsWorldSkillAndPlaceCandidateGuidance`
  - 测试 fixture 的 Haley `skills.json` 加入 `stardew-world`。
  - 测试 fixture 使用目录型 `stardew-world/SKILL.md` 和 `references/stardew-places.md`，不是旧的扁平 `stardew-world.md`。
  - 断言 system prompt 包含 `stardew-world test guidance`。
  - 断言主 skill 只包含 reference 索引，不把完整地点百科塞进 tick prompt。

- `StardewAutonomyTickDebugServiceTests.RunOneTickAsync_WithPlaceCandidateButNoMoveToolCall_DoesNotMove`
  - observation facts 包含 `placeCandidate`。
  - 模型选择等待/说话且不调用 `stardew_move`。
  - 断言 command service 没有收到 move，证明候选不是 host 强制指令。

- `StardewAutonomyTickDebugServiceTests.RunOneTickAsync_NonPrivateChatEventDoesNotDirectlyDriveMove`
  - 输入一个非私聊事件，例如普通 `time_changed` / `player_nearby`。
  - 模型不调用 move 时，host 不得自动提交 move。
  - 断言事件只作为 prompt context 出现。

- `StardewNpcToolFactoryTests.MoveToolDescription_AllowsObservedPlaceCandidate`
  - 断言 `stardew_move` description 和 schema 描述允许使用当前 observation 的 `moveCandidate` 或 `placeCandidate`。
  - 断言 schema 暴露可选 `facingDirection`，用于 schedule-style destination。
  - 断言仍不暴露 `npcId/saveId/traceId/idempotencyKey`。

- `BridgeMoveCommandQueueRegressionTests.MovePumpUsesStardewSchedulePathfindingInsteadOfStraightLineSteps`
  - 断言 bridge move pump 使用 `PathFindController.findPathForNPCSchedules`。
  - 断言旧的 Manhattan `NextStepFrom` 不再驱动 NPC 移动。

- Bridge status 候选测试
  - 若当前 bridge 测试环境难以实例化 SMAPI `NPC/GameLocation`，以 query DTO/facts 单元测试覆盖 core 契约。
  - bridge 侧以编译和现有 bridge 测试保证 DTO 兼容。

## 验证命令
```powershell
dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug
dotnet test .\Mods\StardewHermesBridge.Tests\Mods.StardewHermesBridge.Tests.csproj -c Debug
```

## 手动测试观察点
- 启动 Hermes + Stardew bridge 后，Haley runtime transcript 的 observation facts 应出现 `placeCandidate`。
- Haley 可基于地点候选调用 `stardew_move`。
- 如果 Haley 需要理解地点含义，日志应能看到她通过 `skill_view` 读取 `stardew-world/references/stardew-places.md`，而不是系统 prompt 预先塞入完整地点百科。
- 日志应出现 `task_move_enqueued/running/completed`。
- 如果 Haley 不移动，transcript 应显示她为什么选择 speak/等待，而不是完全不知道可去地点。
