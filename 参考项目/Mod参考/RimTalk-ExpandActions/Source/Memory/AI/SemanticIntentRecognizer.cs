using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimTalkExpandActions.Memory.AI
{
    /// <summary>
    /// 语义意图识别器（核心分析逻辑）
    /// 
    /// 优化版本 v2.0:
    /// 1. 最大值策略 (Max Strategy): 不再计算平均分，取最相似的锚点
    /// 2. 降低阈值: 基准线 0.25，间距 0.03
    /// 3. 关键词加权: 负向关键词降低分数，模糊关键词提升分数
    /// </summary>
    public static class SemanticIntentRecognizer
    {
        /// <summary>
        /// 意图锚点（预定义正向与负向示例）
        /// </summary>
        private static readonly Dictionary<string, IntentAnchor> Anchors = new Dictionary<string, IntentAnchor>
        {
            // 1. 招募同意
            {
                "recruit_agree",
                new IntentAnchor
                {
                    PositiveExamples = new[] 
                    { 
                        "好的我加入", 
                        "我愿意跟着你", 
                        "我同意投靠", 
                        "我同意成为你的人",
                        "我决定跟你走了",
                        "我接受你的招募"
                    },
                    NegativeExamples = new[] 
                    { 
                        "不可能的", 
                        "做梦", 
                        "我不会投靠的",
                        "我拒绝",
                        "别想让我加入"
                    },
                    NegativeKeywords = new[] { "不", "拒绝", "不会", "没", "别想", "算了吧", "做梦", "考虑", "不愿", "犹豫" },
                    AmbiguousKeywords = new[] { "考虑", "想想", "再说", "或许", "可能", "也对", "确实", "有点" },
                    RequiredKeywords = new[] { "加入", "走", "留", "投降", "效忠", "同意", "愿意", "招募", "跟着", "投靠" } // ? 新增
                }
            },
            
            // 2. 恋爱接受
            {
                "romance_accept",
                new IntentAnchor
                {
                    PositiveExamples = new[] 
                    { 
                        "我也喜欢你", 
                        "我们在一起吧", 
                        "我愿意和你在一起",
                        "我爱你",
                        "我接受你的告白",
                        "我也对你有感觉"
                    },
                    NegativeExamples = new[] 
                    { 
                        "我们是朋友", 
                        "我不喜欢你", 
                        "我们不合适",
                        "我拒绝",
                        "对不起我没有这种感觉"
                    },
                    NegativeKeywords = new[] { "不", "拒绝", "朋友", "不合适", "对不起", "没感觉", "距离", "算了" },
                    AmbiguousKeywords = new[] { "考虑", "想想", "或许", "也许", "可能", "时间" },
                    RequiredKeywords = new[] { "爱", "喜欢", "在一起", "交往", "对象", "伴侣", "恋爱", "告白", "感觉" } // ? 新增
                }
            },
            
            // 3. 分手请求
            {
                "romance_breakup",
                new IntentAnchor
                {
                    PositiveExamples = new[] 
                    { 
                        "我们分手吧", 
                        "我不爱你了", 
                        "我们结束吧",
                        "我想分开",
                        "我觉得我们不合适",
                        "我们还是做朋友吧"
                    },
                    NegativeExamples = new[] 
                    { 
                        "我爱你", 
                        "我们在一起吧", 
                        "我不想分手",
                        "我会改的",
                        "请不要离开我"
                    },
                    NegativeKeywords = new[] { "不分手", "不想", "不要", "别离开", "爱你", "在一起" },
                    AmbiguousKeywords = new[] { "考虑", "想想", "冷静", "时间", "空间" },
                    RequiredKeywords = new[] { "分", "结束", "不爱", "离开", "合适", "分手", "分开" } // ? 新增
                }
            },
            
            // 4. 强制休息
            {
                "force_rest",
                new IntentAnchor
                {
                    PositiveExamples = new[] 
                    { 
                        "我好累", 
                        "我要睡觉", 
                        "我需要休息",
                        "撑不住了",
                        "去睡一觉",
                        "我扛不住了"
                    },
                    NegativeExamples = new[] 
                    { 
                        "我不累", 
                        "我精神很好", 
                        "我还能继续",
                        "我不需要休息",
                        "还要坚持下去"
                    },
                    NegativeKeywords = new[] { "不累", "不需要休息", "还能坚持", "精神很好", "不想睡" },
                    AmbiguousKeywords = new[] { "有点", "稍微", "还行", "可以" },
                    RequiredKeywords = new[] { "累", "睡", "困", "休息", "歇", "倒下", "撑不住", "扛不住" } // ? 新增
                }
            },
            
            // 5. 战斗灵感
            {
                "inspire_fight",
                new IntentAnchor
                {
                    PositiveExamples = new[] 
                    { 
                        "我要去战斗", 
                        "我要杀敌", 
                        "我想去杀人",
                        "让战神附体",
                        "我要参战了",
                        "让我冲过去吧"
                    },
                    NegativeExamples = new[] 
                    { 
                        "我不想打仗", 
                        "我害怕战斗", 
                        "我要逃跑",
                        "别让我去",
                        "我不敢"
                    },
                    NegativeKeywords = new[] { "害怕", "逃跑", "不敢", "退缩", "逃走", "被迫" },
                    AmbiguousKeywords = new[] { "战斗", "打架", "冲锋", "杀敌" },
                    RequiredKeywords = new[] { "打", "杀", "战", "枪", "死", "冲", "敌", "战斗", "射击", "攻击" } // ? 新增
                }
            },
            
            // 6. 工作灵感
            {
                "inspire_work",
                new IntentAnchor
                {
                    PositiveExamples = new[] 
                    { 
                        "我想去工作", 
                        "让我干活", 
                        "我要努力劳动",
                        "我干劲十足",
                        "我要高效工作",
                        "给我一堆活吧"
                    },
                    NegativeExamples = new[] 
                    { 
                        "我不想工作", 
                        "要我休息", 
                        "好累啊",
                        "懒得干活",
                        "真懒得动"
                    },
                    NegativeKeywords = new[] { "不想工作", "累了", "懒得", "休息", "疲惫" },
                    AmbiguousKeywords = new[] { "工作", "干活", "努力", "专注" },
                    RequiredKeywords = new[] { "活", "工作", "忙", "干", "造", "修", "建", "制作", "生产" } // ? 新增
                }
            },
            
            // 7. 赠送物品
            {
                "give_item",
                new IntentAnchor
                {
                    PositiveExamples = new[] 
                    { 
                        "这个送给你", 
                        "这是给你的", 
                        "拿去吧",
                        "这是给你的礼物",
                        "送你点东西",
                        "收下这个吧"
                    },
                    NegativeExamples = new[] 
                    { 
                        "我不给", 
                        "这是我的", 
                        "你想要我的东西",
                        "我不会给的",
                        "想得美"
                    },
                    NegativeKeywords = new[] { "不给", "我的", "想得美", "做梦", "别想要" },
                    AmbiguousKeywords = new[] { "可以", "也行", "随便", "看着办" },
                    RequiredKeywords = new[] { "送", "给", "拿", "礼物", "东西", "收下" } // ? 新增
                }
            },
            
            // 8. 社交聚餐
            {
                "social_dining",
                new IntentAnchor
                {
                    PositiveExamples = new[] 
                    { 
                        "我们一起吃饭吧", 
                        "一起吃晚饭", 
                        "咱们共进晚餐",
                        "一起去吃饭",
                        "陪我吃个饭",
                        "咱们聚餐吧"
                    },
                    NegativeExamples = new[] 
                    { 
                        "我不饿", 
                        "你自己吃吧", 
                        "我不想一起吃",
                        "别打扰我",
                        "我没胃口"
                    },
                    NegativeKeywords = new[] { "不饿", "自己吃", "不想", "打扰", "没胃口" },
                    AmbiguousKeywords = new[] { "可以", "也行", "随便", "都行" },
                    RequiredKeywords = new[] { "吃", "餐", "饭", "饿", "食", "聚餐", "用餐" } // ? 新增
                }
            },
            
            // 9. 社交娱乐
            {
                "social_relax",
                new IntentAnchor
                {
                    PositiveExamples = new[] 
                    { 
                        "我们一起玩吧", 
                        "一起娱乐吧", 
                        "一起放松一下",
                        "陪我玩一会儿",
                        "出去玩玩",
                        "咱们出去走走"
                    },
                    NegativeExamples = new[] 
                    { 
                        "我不想玩", 
                        "我要工作", 
                        "没空",
                        "我没兴趣",
                        "我很忙"
                    },
                    NegativeKeywords = new[] { "不想", "没空", "忙", "没兴趣", "很忙", "工作" },
                    AmbiguousKeywords = new[] { "可以", "也行", "随便", "看情况", "再说" },
                    RequiredKeywords = new[] { "玩", "聊", "逛", "娱乐", "放松", "消遣", "休闲" } // ? 新增
                }
            }
        };

        /// <summary>
        /// 相似度阈值设置，优化后的数值：
        /// v2.2: 显著提高阈值以减少误触
        /// </summary>
        private const float POSITIVE_THRESHOLD = 0.45f;    // 最低相似度阈值（提升至 0.45）
        private const float GAP_THRESHOLD = 0.10f;         // 正负样本最小差距（提升至 0.10）
        private const float CONFIDENCE_THRESHOLD = 0.50f;  // 最终置信度阈值（新增）
        
        /// <summary>
        /// 关键词加权设置
        /// v2.2: 增强负面关键词的惩罚力度
        /// </summary>
        private const float NEGATIVE_KEYWORD_PENALTY = -0.30f;  // 负面关键词惩罚（加倍）
        private const float AMBIGUOUS_KEYWORD_BOOST = 0.03f;    // 模糊关键词加成（降低）
        private const float REQUIRED_KEYWORD_BOOST = 0.08f;     // 必需关键词加成（新增）

        /// <summary>
        /// 意图锚点数据结构
        /// 
        /// v2.1 新增：RequiredKeywords（话题领域过滤）
        /// </summary>
        private class IntentAnchor
        {
            public string[] PositiveExamples;
            public string[] NegativeExamples;
            public string[] NegativeKeywords;     // 负向关键词（降低分数）
            public string[] AmbiguousKeywords;    // 模糊关键词（提升分数）
            public string[] RequiredKeywords;     // 必含关键词（话题领域过滤）? 新增
            public List<float[]> PositiveEmbeddings;
            public List<float[]> NegativeEmbeddings;
            public bool IsLoaded = false;
        }

        /// <summary>
        /// 异步分析文本意图（入口方法）
        /// </summary>
        public static void AnalyzeAsync(string text, Pawn speaker, Pawn listener)
        {
            try
            {
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message("[RimTalk-ExpandActions] ━━━━━━━━ SemanticIntentRecognizer.AnalyzeAsync 开始 ━━━━━━━━");
                }
                
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                    {
                        Log.Warning("[RimTalk-ExpandActions] SemanticIntentRecognizer: 文本为空，跳过分析");
                    }
                    return;
                }

                if (speaker == null)
                {
                    if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                    {
                        Log.Warning("[RimTalk-ExpandActions] SemanticIntentRecognizer: speaker 为 null，跳过分析");
                    }
                    return;
                }

                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[RimTalk-ExpandActions] 分析文本: {text}");
                    Log.Message($"[RimTalk-ExpandActions] 说话者: {speaker.Name.ToStringShort} (派系: {speaker.Faction?.Name ?? "无"})");
                    Log.Message($"[RimTalk-ExpandActions] 听众: {listener?.Name?.ToStringShort ?? "null"}");
                    Log.Message("[RimTalk-ExpandActions] 正在确保锚点向量已加载...");
                }

                // 确保锚点向量已加载
                EnsureAnchorsLoaded(() =>
                {
                    if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                    {
                        Log.Message("[RimTalk-ExpandActions] ? 锚点向量已就绪，开始获取文本向量...");
                    }
                    
                    // 调用 EmbeddingService 获取文本向量
                    EmbeddingService.GetEmbedding(
                        text,
                        onSuccess: (embedding) => 
                        {
                            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                            {
                                Log.Message($"[RimTalk-ExpandActions] ? 成功获取文本向量（维度: {embedding?.Length ?? 0}）");
                            }
                            OnEmbeddingReceived(embedding, text, speaker, listener);
                        },
                        onFailure: (error) => 
                        {
                            Log.Error($"[RimTalk-ExpandActions] ? 获取向量失败: {error}");
                            OnEmbeddingFailed(error, text);
                        }
                    );
                });
                
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message("[RimTalk-ExpandActions] ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] SemanticIntentRecognizer.AnalyzeAsync 失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 确保所有锚点向量已加载
        /// </summary>
        private static void EnsureAnchorsLoaded(Action onComplete)
        {
            bool allLoaded = Anchors.Values.All(a => a.IsLoaded);

            if (allLoaded)
            {
                onComplete?.Invoke();
                return;
            }

            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message("[RimTalk-ExpandActions] SemanticIntentRecognizer: 首次使用，开始加载锚点向量...");
            }
            LoadAnchorsAsync(onComplete);
        }

        /// <summary>
        /// 异步加载所有锚点的向量
        /// </summary>
        private static void LoadAnchorsAsync(Action onComplete)
        {
            int pendingCount = 0;
            int totalCount = 0;
            int successCount = 0;

            foreach (var anchor in Anchors.Values)
            {
                totalCount += anchor.PositiveExamples.Length + anchor.NegativeExamples.Length;
            }

            pendingCount = totalCount;
            
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message($"[RimTalk-ExpandActions] 需要加载 {totalCount} 个锚点向量");
            }

            Action checkComplete = () =>
            {
                pendingCount--;
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[RimTalk-ExpandActions] 锚点加载进度: {totalCount - pendingCount}/{totalCount}");
                }
                
                if (pendingCount <= 0)
                {
                    if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                    {
                        Log.Message($"[RimTalk-ExpandActions] ? 所有锚点向量加载完成 (成功: {successCount}/{totalCount})");
                    }
                    
                    foreach (var anchor in Anchors.Values)
                    {
                        anchor.IsLoaded = true;
                    }
                    
                    onComplete?.Invoke();
                }
            };

            foreach (var anchorEntry in Anchors)
            {
                IntentAnchor anchor = anchorEntry.Value;
                anchor.PositiveEmbeddings = new List<float[]>();
                anchor.NegativeEmbeddings = new List<float[]>();

                // 加载正向示例
                foreach (string example in anchor.PositiveExamples)
                {
                    EmbeddingService.GetEmbedding(
                        example,
                        onSuccess: (emb) => 
                        { 
                            anchor.PositiveEmbeddings.Add(emb);
                            successCount++;
                            checkComplete(); 
                        },
                        onFailure: (err) => 
                        { 
                            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                            {
                                Log.Error($"[RimTalk-ExpandActions] ? 锚点加载失败: '{example}' - {err}");
                            }
                            checkComplete(); 
                        }
                    );
                }

                // 加载负向示例
                foreach (string example in anchor.NegativeExamples)
                {
                    EmbeddingService.GetEmbedding(
                        example,
                        onSuccess: (emb) => 
                        { 
                            anchor.NegativeEmbeddings.Add(emb);
                            successCount++;
                            checkComplete(); 
                        },
                        onFailure: (err) => 
                        { 
                            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                            {
                                Log.Error($"[RimTalk-ExpandActions] ? 锚点加载失败: '{example}' - {err}");
                            }
                            checkComplete(); 
                        }
                    );
                }
            }
        }

        /// <summary>
        /// 当嵌入获取成功回调，优化版：最大值策略 + 关键词加权 + 必需关键词
        /// 
        /// v2.2 更严格的关键词机制：
        /// - 必需关键词检查作为第一道防线（前置检查）
        /// - 负面关键词作为强制否决（veto power）
        /// - 多阶段过滤确保高精度
        /// </summary>
        private static void OnEmbeddingReceived(float[] textEmbedding, string text, Pawn speaker, Pawn listener)
        {
            try
            {
                string matchedIntent = null;
                float bestScore = 0f;
                Dictionary<string, float> intentScores = new Dictionary<string, float>();

                foreach (var anchor in Anchors)
                {
                    string intentName = anchor.Key;
                    IntentAnchor intentData = anchor.Value;

                    if (!intentData.IsLoaded)
                    {
                        continue;
                    }

                    // ===== 阶段 1: 负面关键词强制否决 =====
                    if (intentData.NegativeKeywords != null && intentData.NegativeKeywords.Length > 0)
                    {
                        bool hasNegativeKeyword = intentData.NegativeKeywords.Any(keyword => text.Contains(keyword));
                        
                        if (hasNegativeKeyword)
                        {
                            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                            {
                                string matchedNegative = intentData.NegativeKeywords.First(k => text.Contains(k));
                                Log.Message($"[RimTalk-ExpandActions] ? 意图 '{intentName}' 被否决（检测到负面关键词: '{matchedNegative}'）");
                            }
                            continue; // 直接跳过此意图
                        }
                    }

                    // ===== 阶段 2: 必需关键词前置检查 =====
                    // 如果定义了必需关键词，文本中必须包含至少一个，否则跳过该意图
                    if (intentData.RequiredKeywords != null && intentData.RequiredKeywords.Length > 0)
                    {
                        bool hasRequiredKeyword = intentData.RequiredKeywords.Any(keyword => text.Contains(keyword));
                        
                        if (!hasRequiredKeyword)
                        {
                            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                            {
                                Log.Message($"[RimTalk-ExpandActions] ○ 跳过意图 '{intentName}'（缺少必需关键词 [{string.Join(", ", intentData.RequiredKeywords)}]）");
                            }
                            continue; // 直接跳过不符合条件的意图
                        }
                    }

                    // ===== 阶段 3: 向量相似度计算 =====
                    // 优化 1: 使用最大值策略（而非使用平均值）
                    float positiveScore = CalculateMaxSimilarity(textEmbedding, intentData.PositiveEmbeddings);
                    float negativeScore = CalculateMaxSimilarity(textEmbedding, intentData.NegativeEmbeddings);

                    // ===== 阶段 4: 关键词加权调整 =====
                    float keywordAdjustment = CalculateKeywordAdjustment(text, intentData);
                    float adjustedPositiveScore = positiveScore + keywordAdjustment;

                    if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                    {
                        Log.Message($"[RimTalk-ExpandActions] 意图 '{intentName}':");
                        Log.Message($"[RimTalk-ExpandActions]   正向(Max)={positiveScore:F3}, 负向(Max)={negativeScore:F3}");
                        Log.Message($"[RimTalk-ExpandActions]   关键词调整={keywordAdjustment:F3}, 调整后得分={adjustedPositiveScore:F3}");
                    }

                    // ===== 阶段 5: 多重阈值验证 =====
                    // 必须同时满足：
                    // 1. 调整后得分 > POSITIVE_THRESHOLD (0.45)
                    // 2. 调整后得分 > 负向得分 + GAP_THRESHOLD (0.10)
                    // 3. 调整后得分 > CONFIDENCE_THRESHOLD (0.50) - 最终置信度
                    if (adjustedPositiveScore > POSITIVE_THRESHOLD && 
                        adjustedPositiveScore > negativeScore + GAP_THRESHOLD &&
                        adjustedPositiveScore > CONFIDENCE_THRESHOLD)
                    {
                        intentScores[intentName] = adjustedPositiveScore;
                        
                        if (adjustedPositiveScore > bestScore)
                        {
                            bestScore = adjustedPositiveScore;
                            matchedIntent = intentName;
                        }
                    }
                    else
                    {
                        if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                        {
                            Log.Message($"[RimTalk-ExpandActions]   → 未通过阈值验证 (需要 >{POSITIVE_THRESHOLD:F2}, >{negativeScore + GAP_THRESHOLD:F2}, >{CONFIDENCE_THRESHOLD:F2})");
                        }
                    }
                }

                // ===== 阶段 6: 意图冲突检测 =====
                if (matchedIntent != null && intentScores.Count > 1)
                {
                    // 检查是否有其他意图得分接近最高分（差距 < 0.08）
                    var competitors = intentScores.Where(kvp => 
                        kvp.Key != matchedIntent && 
                        Math.Abs(kvp.Value - bestScore) < 0.08f
                    ).ToList();

                    if (competitors.Any())
                    {
                        if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                        {
                            Log.Warning($"[RimTalk-ExpandActions] ? 意图冲突检测: 最佳意图 '{matchedIntent}' ({bestScore:F3}) 与其他意图得分接近:");
                            foreach (var comp in competitors)
                            {
                                Log.Warning($"[RimTalk-ExpandActions]   - '{comp.Key}' ({comp.Value:F3}, 差距: {Math.Abs(bestScore - comp.Value):F3})");
                            }
                            Log.Warning("[RimTalk-ExpandActions] → 放弃执行以避免误触");
                        }
                        return; // 意图不明确，放弃执行
                    }
                }

                if (matchedIntent != null)
                {
                    Log.Message($"[RimTalk-ExpandActions] ? 成功匹配意图: {matchedIntent} (置信度: {bestScore:F3})");
                    ActionExecutor.Execute(matchedIntent, speaker, listener);
                }
                else
                {
                    if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                    {
                        Log.Message("[RimTalk-ExpandActions] 未匹配到任何意图（所有候选项均未通过多重阈值验证）");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] SemanticIntentRecognizer.OnEmbeddingReceived 失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 向量获取失败回调
        /// </summary>
        private static void OnEmbeddingFailed(string error, string text)
        {
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Warning($"[RimTalk-ExpandActions] SemanticIntentRecognizer: 向量获取失败: {error}");
                Log.Warning($"[RimTalk-ExpandActions] 原始文本: {text}");
            }
        }

        /// <summary>
        /// ? 优化 1: 计算最大相似度（不再使用平均值）
        /// 只要有一个锚点很相似，就判定为命中
        /// </summary>
        private static float CalculateMaxSimilarity(float[] textEmbedding, List<float[]> exampleEmbeddings)
        {
            if (exampleEmbeddings == null || exampleEmbeddings.Count == 0)
            {
                return 0f;
            }

            float maxSimilarity = 0f;

            foreach (var exampleEmbedding in exampleEmbeddings)
            {
                if (exampleEmbedding != null && exampleEmbedding.Length > 0)
                {
                    float similarity = VectorUtils.CosineSimilarity(textEmbedding, exampleEmbedding);
                    if (similarity > maxSimilarity)
                    {
                        maxSimilarity = similarity;
                    }
                }
            }

            return maxSimilarity;
        }

        /// <summary>
        /// ★ 优化 3: 关键词加权
        /// 根据文本中是否包含正向/模糊关键词，调整得分
        /// v2.2: 负面关键词现在在前置阶段强制否决，这里只计算正向加成
        /// </summary>
        private static float CalculateKeywordAdjustment(string text, IntentAnchor anchor)
        {
            float adjustment = 0f;
            int requiredKeywordCount = 0;
            int ambiguousKeywordCount = 0;

            // 必需关键词加成（已在前置检查通过，这里给予奖励）
            if (anchor.RequiredKeywords != null)
            {
                foreach (string keyword in anchor.RequiredKeywords)
                {
                    if (text.Contains(keyword))
                    {
                        requiredKeywordCount++;
                        adjustment += REQUIRED_KEYWORD_BOOST;
                        
                        if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                        {
                            Log.Message($"[RimTalk-ExpandActions]   ? 检测到必需关键词: '{keyword}' (+{REQUIRED_KEYWORD_BOOST:F3})");
                        }
                    }
                }
            }

            // 模糊关键词：轻微增强（暗示相关性）
            if (anchor.AmbiguousKeywords != null)
            {
                foreach (string keyword in anchor.AmbiguousKeywords)
                {
                    if (text.Contains(keyword))
                    {
                        ambiguousKeywordCount++;
                        adjustment += AMBIGUOUS_KEYWORD_BOOST;
                        
                        if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                        {
                            Log.Message($"[RimTalk-ExpandActions]   ○ 检测到模糊关键词: '{keyword}' (+{AMBIGUOUS_KEYWORD_BOOST:F3})");
                        }
                    }
                }
            }

            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true && (requiredKeywordCount > 0 || ambiguousKeywordCount > 0))
            {
                Log.Message($"[RimTalk-ExpandActions]   关键词统计: 必需={requiredKeywordCount}, 模糊={ambiguousKeywordCount}, 总调整={adjustment:F3}");
            }

            return adjustment;
        }
    }
}
