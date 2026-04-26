using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimTalkExpandActions.Memory.AI.IntentRules;

namespace RimTalkExpandActions.Memory.AI
{
    /// <summary>
    /// 延迟行为执行队列
    /// 管理待执行的行为，提供自然延迟和取消机制
    /// </summary>
    public class DelayedActionQueue : GameComponent
    {
        // 待执行的行为队列
        private List<PendingAction> _pendingActions = new List<PendingAction>();
        private object _lock = new object();
        
        // 已完成的行为（用于防止重复）
        private HashSet<string> _recentlyExecuted = new HashSet<string>();
        private float _cleanupTimer = 0f;
        private const float CLEANUP_INTERVAL = 60f; // 每60秒清理一次
        
        public DelayedActionQueue(Game game) : base()
        {
        }
        
        /// <summary>
        /// 待执行行为
        /// </summary>
        public class PendingAction
        {
            /// <summary>唯一ID</summary>
            public string Id { get; set; }
            
            /// <summary>意图ID（对应ActionExecutor）</summary>
            public string IntentId { get; set; }
            
            /// <summary>显示名称</summary>
            public string DisplayName { get; set; }
            
            /// <summary>执行者</summary>
            public Pawn Actor { get; set; }
            
            /// <summary>目标</summary>
            public Pawn Target { get; set; }
            
            /// <summary>总延迟时间（秒）</summary>
            public float TotalDelay { get; set; }
            
            /// <summary>剩余时间（秒）</summary>
            public float TimeRemaining { get; set; }
            
            /// <summary>是否可取消</summary>
            public bool IsCancellable { get; set; } = true;
            
            /// <summary>是否已取消</summary>
            public bool IsCancelled { get; set; } = false;
            
            /// <summary>创建时间</summary>
            public float CreatedAt { get; set; }
            
            /// <summary>风险等级</summary>
            public RiskLevel RiskLevel { get; set; }
            
            /// <summary>置信度</summary>
            public float Confidence { get; set; }
        }
        
        /// <summary>
        /// 添加待执行行为到队列
        /// </summary>
        public void Enqueue(
            string intentId,
            string displayName,
            Pawn actor,
            Pawn target,
            float delaySeconds,
            RiskLevel riskLevel,
            float confidence)
        {
            if (string.IsNullOrEmpty(intentId) || actor == null)
            {
                Log.Warning("[DelayedActionQueue] 无效的行为参数");
                return;
            }
            
            // 生成唯一ID
            string actionId = $"{intentId}_{actor.ThingID}_{target?.ThingID ?? "null"}_{Time.time}";
            
            lock (_lock)
            {
                // 检查是否有重复的待执行行为
                if (_pendingActions.Exists(a =>
                    a.IntentId == intentId &&
                    a.Actor == actor &&
                    a.Target == target &&
                    !a.IsCancelled))
                {
                    if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                    {
                        Log.Message($"[DelayedActionQueue] 跳过重复行为: {displayName}");
                    }
                    return;
                }
                
                // 添加随机延迟波动（±20%）
                float randomFactor = UnityEngine.Random.Range(0.8f, 1.2f);
                float actualDelay = delaySeconds * randomFactor;
                
                var pendingAction = new PendingAction
                {
                    Id = actionId,
                    IntentId = intentId,
                    DisplayName = displayName,
                    Actor = actor,
                    Target = target,
                    TotalDelay = actualDelay,
                    TimeRemaining = actualDelay,
                    IsCancellable = true,
                    CreatedAt = Time.time,
                    RiskLevel = riskLevel,
                    Confidence = confidence
                };
                
                _pendingActions.Add(pendingAction);
                
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[DelayedActionQueue] ✓ 添加待执行行为");
                    Log.Message($"[DelayedActionQueue]   行为: {displayName}");
                    Log.Message($"[DelayedActionQueue]   执行者: {actor.LabelShort}");
                    Log.Message($"[DelayedActionQueue]   目标: {target?.LabelShort ?? "无"}");
                    Log.Message($"[DelayedActionQueue]   延迟: {actualDelay:F1} 秒");
                }
            }
        }
        
        /// <summary>
        /// 取消待执行行为
        /// </summary>
        public bool Cancel(string actionId)
        {
            lock (_lock)
            {
                var action = _pendingActions.Find(a => a.Id == actionId);
                if (action != null && action.IsCancellable && !action.IsCancelled)
                {
                    action.IsCancelled = true;
                    Log.Message($"[DelayedActionQueue] ✗ 已取消行为: {action.DisplayName}");
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// 取消指定角色的所有待执行行为
        /// </summary>
        public int CancelAllForPawn(Pawn pawn)
        {
            int count = 0;
            lock (_lock)
            {
                foreach (var action in _pendingActions)
                {
                    if ((action.Actor == pawn || action.Target == pawn) &&
                        action.IsCancellable &&
                        !action.IsCancelled)
                    {
                        action.IsCancelled = true;
                        count++;
                    }
                }
            }
            
            if (count > 0)
            {
                Log.Message($"[DelayedActionQueue] 已取消 {pawn.LabelShort} 的 {count} 个待执行行为");
            }
            
            return count;
        }
        
        /// <summary>
        /// 获取指定角色的待执行行为列表
        /// </summary>
        public List<PendingAction> GetPendingActionsForPawn(Pawn pawn)
        {
            lock (_lock)
            {
                return _pendingActions.FindAll(a =>
                    (a.Actor == pawn || a.Target == pawn) &&
                    !a.IsCancelled);
            }
        }
        
        /// <summary>
        /// 每帧更新
        /// </summary>
        public override void GameComponentTick()
        {
            lock (_lock)
            {
                if (_pendingActions.Count == 0)
                    return;
                
                float deltaTime = 1f / 60f; // 假设60 TPS
                
                // 更新所有待执行行为
                for (int i = _pendingActions.Count - 1; i >= 0; i--)
                {
                    var action = _pendingActions[i];
                    
                    // ... (rest of the loop content)
                
                    // 跳过已取消的
                    if (action.IsCancelled)
                    {
                        _pendingActions.RemoveAt(i);
                        continue;
                    }
                    
                    // 验证角色有效性
                    if (action.Actor == null || action.Actor.Dead || action.Actor.Destroyed)
                    {
                        _pendingActions.RemoveAt(i);
                        continue;
                    }
                    
                    // 更新剩余时间
                    action.TimeRemaining -= deltaTime;
                    
                    // 时间到，执行行为
                    if (action.TimeRemaining <= 0)
                    {
                        // 在锁内移除，但在锁外执行？
                        // 不，ActionExecutor 可能会耗时，但不应该阻塞太久。
                        // 如果 ExecuteAction 导致入队，会死锁吗？
                        // ActionExecutor.Execute 不会调用 Enqueue。
                        
                        _pendingActions.RemoveAt(i);
                        
                        // 记录已执行
                        _recentlyExecuted.Add(action.Id);
                        
                        // 执行行为 (放在锁外可能更好，但为了简化逻辑，先放在这里)
                        // 注意：如果 ExecuteAction 抛出异常，锁会被释放（因为在 lock 块内）
                        ExecuteAction(action);
                    }
                }
                
                // 定期清理已执行记录
                _cleanupTimer += deltaTime;
                if (_cleanupTimer >= CLEANUP_INTERVAL)
                {
                    _recentlyExecuted.Clear();
                    _cleanupTimer = 0f;
                }
            }
        }
        
        /// <summary>
        /// 执行行为
        /// </summary>
        private void ExecuteAction(PendingAction action)
        {
            try
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[DelayedActionQueue] ★ 执行行为: {action.DisplayName}");
                    Log.Message($"[DelayedActionQueue]   执行者: {action.Actor.LabelShort}");
                    Log.Message($"[DelayedActionQueue]   目标: {action.Target?.LabelShort ?? "无"}");
                    Log.Message($"[DelayedActionQueue]   延迟完成: {action.TotalDelay:F1} 秒");
                }
                
                // 调用ActionExecutor执行
                ActionExecutor.Execute(action.IntentId, action.Actor, action.Target);
            }
            catch (Exception ex)
            {
                Log.Error($"[DelayedActionQueue] 执行行为失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 获取队列状态信息（用于调试）
        /// </summary>
        public string GetQueueStatus()
        {
            if (_pendingActions.Count == 0)
            {
                return "延迟队列: 空";
            }
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"延迟队列: {_pendingActions.Count} 个待执行");
            
            foreach (var action in _pendingActions)
            {
                if (!action.IsCancelled)
                {
                    sb.AppendLine($"  - {action.DisplayName}: {action.TimeRemaining:F1}s ({action.Actor.LabelShort})");
                }
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 保存数据
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            // 待执行行为不需要持久化，重启游戏后自然清空
        }
    }
    
    /// <summary>
    /// 延迟队列管理器（静态访问入口）
    /// </summary>
    public static class DelayedActionQueueManager
    {
        private static DelayedActionQueue _instance;
        
        /// <summary>
        /// 获取队列实例
        /// </summary>
        public static DelayedActionQueue Instance
        {
            get
            {
                if (_instance == null && Current.Game != null)
                {
                    _instance = Current.Game.GetComponent<DelayedActionQueue>();
                    if (_instance == null)
                    {
                        Log.Warning("[DelayedActionQueue] 未找到GameComponent，创建新实例");
                        _instance = new DelayedActionQueue(Current.Game);
                    }
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// 添加待执行行为（便捷方法）
        /// </summary>
        public static void Enqueue(
            string intentId,
            string displayName,
            Pawn actor,
            Pawn target,
            float delaySeconds,
            RiskLevel riskLevel = RiskLevel.Medium,
            float confidence = 0.8f)
        {
            Instance?.Enqueue(intentId, displayName, actor, target, delaySeconds, riskLevel, confidence);
        }
        
        /// <summary>
        /// 重置实例（游戏重新加载时）
        /// </summary>
        public static void Reset()
        {
            _instance = null;
        }
    }
}