# Cloud Control Operator Console Check

Date: 2026-03-29
Change: `cloudcontrol-operator-console-ui`
Scope: operator-console startup, page routing, private-dialogue operator flow, narrative/memory/support/outcome pages

## Status In Parent Proposal

- 自 `cloudcontrol-platform-control-plane` 父提案落地后，本 change 只应被视为 `M1` 首波 workbench 切片来源。
- 它不再是完整 `Cloud Control` 控制面 IA 的真相源。
- 后续页面级 IA、商业、通知、系统治理统一以 `cloudcontrol-platform-control-plane` 为准；本 check 保留为旧 workbench 验证记录。

## Automated Verification

Command:

```powershell
dotnet test tests/Superpowers.Runtime.Tests/Superpowers.Runtime.Tests.csproj --filter "OperatorConsoleHostTests|OperatorConsoleAccessPolicyTests|OperatorConsolePrivateDialogueTests|OperatorConsoleStateSupportTests"
```

Expected result:

- `OperatorConsoleHostTests` verifies root/operator-console routing, overview state, owner boundary copy, and shared navigation shell.
- `OperatorConsoleAccessPolicyTests` verifies loopback-only access policy plus degraded overview fail-closed render.
- `OperatorConsolePrivateDialogueTests` verifies provider input form, candidate generation, accepted actions/deterministic outcomes, create/finalize/recover, confirmation gate, and provider fail-closed states.
- `OperatorConsoleStateSupportTests` verifies narrative-state, memory, support, outcome pages, plus future-blueprint navigation exclusion.

Observed result:

- Passing on 2026-03-29 in local workspace.
- `33` tests passed, `0` failed.

## Page-Level Checks

Startup:

- `Cloud Control Operator Console` root page loads instead of `404`.
- Root shell declares `loopback-only / local-only`.
- Root shell declares it is not the player-facing `Launcher`.

Private Dialogue:

- Provider-generation input form exposes current generation inputs.
- Candidate result shows normalized content, normalized actions, accepted actions, and deterministic outcomes.
- `create` / `finalize` / `recover` all require explicit confirmation.
- Write result surfaces request trace, canonical record id, replay state, reason/failure fields.
- Provider failure modes render fail-closed reason codes.

Narrative State:

- Search supports `canonicalRecordId`, `actorId`, and `replayState`.
- Results show canonical replay, pending visible, mirrored writeback, and committed-memory-adjacent signal.

Memory:

- Dedicated page states that it is the current `M1` server-owned memory surface.
- Page shows memory summary and committed turns.

Support:

- Page supports support-ticket submit and search by `receiptId`, `failureClass`, and `createdAfterUtc`.
- Results show ticket status and recovery entry.

Outcome / Commitment:

- Dedicated page shows accepted actions and deterministic outcomes from server-owned replay state.
- Search supports `canonicalRecordId` and `actorId`.

## Boundary Checks

- Navigation is limited to:
  - `Overview`
  - `Private Dialogue`
  - `Narrative State`
  - `Memory`
  - `Support`
  - `Outcome / Commitment`
- Console does not expose future blueprint pages such as `Billing`, `Marketplace`, or `Entitlements`.
