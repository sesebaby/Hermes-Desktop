# Player-Visible Verification Schema

状态：

- active design baseline

owner：

- release governance owner

用途：

- 为所有 player-visible `Launcher / Mod / web` surface 定义统一的可视化验收证据结构
- 阻止“代码完成但人眼不可见 / 不可交互”被误记为完成

适用范围：

- `Launcher`
- 游戏内 `Mod` surface
- browser-delivered `web` surface

required fields：

- `artifactPath`
- `buildRevision`
- `surfaceId`
- `visibleHost`
- `entryPath`
- `startupProof`
- `visibleSurfaceProof`
- `interactionProof`
- `visualEvidenceRef`
- `reviewer`
- `reviewTimestamp`
- `result`

field rules：

- `artifactPath`
  - 指向被验证的程序、mod、页面或治理对象
- `buildRevision`
  - 必须绑定具体 commit / candidate revision
- `surfaceId`
  - 指向被验证的玩家可见 surface
- `visibleHost`
  - 必须命名玩家实际看到它的宿主
- `entryPath`
  - 必须写清玩家或 reviewer 实际如何进入该 surface
- `startupProof`
  - 必须证明：
    - 程序 / 页面 / 宿主游戏真实启动
    - 目标窗口 / 页面 / surface 确实存在
- `visibleSurfaceProof`
  - 必须证明：
    - 目标 surface 真正对人眼可见
    - 不是只在日志、对象状态、后台窗口或不可见 shell 中成立
- `interactionProof`
  - 必须至少包含 1 条真实玩家动作与预期结果
- `visualEvidenceRef`
  - 必须指向：
    - 截图
    - 或经批准的等价可视证据
- `reviewer`
  - 不得为该改动的唯一实现者
- `result`
  - 当前固定为：
    - `passed`
    - `failed`
    - `visual_gate_pending`

hard rules：

- 没有 `startupProof`，不得宣称 player-visible 功能完成
- 没有 `visibleSurfaceProof`，不得宣称 player-visible 功能完成
- 没有 `interactionProof`，不得宣称 player-visible 功能完成
- 没有 `visualEvidenceRef`，不得宣称 player-visible 功能完成
- 若 `result != passed`，不得将该 surface 记为 `done`

minimum examples：

- `Launcher`
  - 启动 exe
  - 窗口存在
  - 截图可见
  - 点击至少 1 个主导航或主 CTA
- `Mod`
  - 启动宿主游戏与 SMAPI
  - 进入真实存档
  - 触发热键 / 菜单入口
  - 截图可见
  - 关闭或切换一次
- `web`
  - 打开目标 URL
  - 页面可见
  - 至少执行 1 个关键交互
  - 截图留证
