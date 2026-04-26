using Verse;
using RimTalkExpandActions.Memory.Utils;
using RimTalkExpandActions.Patches;

namespace RimTalkExpandActions
{
    /// <summary>
    /// 游戏组件：定期更新 Pawn 缓存
    /// </summary>
    public class RimTalkExpandActionsGameComponent : GameComponent
    {
        public RimTalkExpandActionsGameComponent(Game game)
        {
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // 每 tick 都调用，但内部会控制更新频率
            ThreadSafePawnCache.Instance.UpdateCache();
        }

        public override void GameComponentOnGUI()
        {
            base.GameComponentOnGUI();
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            ThreadSafePawnCache.Instance.ClearCache();
            JobTriggerPatch.ClearCooldowns();
            Log.Message("[RimTalk-ExpandActions] 新游戏已启动，Pawn 缓存和 Job 触发器冷却已重置");
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            ThreadSafePawnCache.Instance.ClearCache();
            JobTriggerPatch.ClearCooldowns();
            Log.Message("[RimTalk-ExpandActions] 游戏已加载，Pawn 缓存和 Job 触发器冷却已重置");
        }
    }
}
