using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimTalkExpandActions.UI
{
    /// <summary>
    /// Job 触发器配置窗口 - 使用纯 Widgets API
    /// 优化版本：支持分类、搜索、批量操作
    /// </summary>
    public class Window_JobTriggerSettings : Window
    {
        // 布局常量
        private const float MARGIN = 10f;
        private const float LINE_HEIGHT = 24f;
        private const float BUTTON_HEIGHT = 28f;
        private const float ROW_HEIGHT = 26f;
        private const float GAP = 6f;
        private const float CHECKBOX_SIZE = 24f;
        
        // UI 状态
        private Vector2 scrollPosition = Vector2.zero;
        private string searchFilter = "";
        private bool showEnabledOnly = false;
        private string selectedCategory = "all";
        
        // 数据
        private List<JobDef> allJobs = null;
        private List<JobDef> filteredJobs = null;
        private Dictionary<string, List<JobDef>> categorizedJobs = null;
        private bool initialized = false;
        
        // 分类定义
        private static readonly Dictionary<string, string> CategoryNames = new Dictionary<string, string>
        {
            { "all", "全部" },
            { "social", "社交" },
            { "work", "工作" },
            { "combat", "战斗" },
            { "movement", "移动" },
            { "basic", "基础" },
            { "other", "其他" }
        };
        
        // Job 分类关键词
        private static readonly Dictionary<string, string[]> CategoryKeywords = new Dictionary<string, string[]>
        {
            { "social", new[] { "Social", "Chat", "Talk", "Interact", "Romance", "Lovin", "Party" } },
            { "work", new[] { "DoBill", "Research", "Construct", "Repair", "Clean", "Haul", "Mine", "Grow", "Harvest", "Hunt", "Butcher", "Cook", "Craft", "Tailor", "Smith", "Art" } },
            { "combat", new[] { "Attack", "Fight", "Kill", "Shoot", "Melee", "Flee", "Hunt" } },
            { "movement", new[] { "Goto", "Follow", "Wait", "Stand", "Wander", "Walk" } },
            { "basic", new[] { "Ingest", "Eat", "Drink", "Sleep", "LayDown", "Rest", "Meditate", "Pray" } }
        };
        
        // Job 中文翻译字典
        private static readonly Dictionary<string, string> JobTranslations = new Dictionary<string, string>
        {
            // 基础行为
            { "Wait", "等待" },
            { "Wait_MaintainPosture", "保持姿势等待" },
            { "Wait_Wander", "徘徊等待" },
            { "Wait_Combat", "战斗等待" },
            { "Wait_Downed", "倒地等待" },
            { "Wait_SafeTemperature", "等待安全温度" },
            { "Wait_Asleep", "睡眠等待" },
            { "GotoWander", "前往徘徊" },
            { "Goto", "前往" },
            { "Follow", "跟随" },
            { "FollowClose", "紧密跟随" },
            
            // 社交行为
            { "SocialRelax", "社交放松" },
            { "StandAndBeSociallyActive", "站立社交" },
            { "Chitchat", "闲聊" },
            { "SpectateCeremony", "观看仪式" },
            { "Lovin", "亲热" },
            { "TryRomance", "尝试浪漫" },
            { "MarryAdjacentPawn", "与相邻殖民者结婚" },
            { "GiveSpeech", "发表演讲" },
            { "UsePath", "使用路径" },
            
            // 饮食
            { "Ingest", "进食" },
            { "FoodDeliver", "递送食物" },
            { "FoodFeedPatient", "喂食病人" },
            { "DropEquipment", "放下装备" },
            
            // 休息
            { "LayDown", "躺下" },
            { "LayDownResting", "躺下休息" },
            { "LayDownAwake", "躺下保持清醒" },
            { "Meditate", "冥想" },
            { "Pray", "祈祷" },
            { "Reign", "统治" },
            
            // 工作
            { "DoBill", "执行配方" },
            { "Research", "研究" },
            { "Deconstruct", "解构" },
            { "Uninstall", "卸载" },
            { "FinishFrame", "完成建筑框架" },
            { "RemoveFloor", "移除地板" },
            { "BuildRoof", "建造屋顶" },
            { "RemoveRoof", "移除屋顶" },
            { "Mine", "采矿" },
            { "OperateDeepDrill", "操作深钻" },
            { "OperateScanner", "操作扫描仪" },
            { "Repair", "修理" },
            { "Smooth", "平整地面" },
            { "Clean", "清洁" },
            { "Haul", "搬运" },
            { "HaulToCell", "搬运到格子" },
            { "HaulToContainer", "搬运到容器" },
            { "HaulCorpseToPublicPlace", "搬运尸体到公共场所" },
            { "Strip", "剥夺" },
            { "Wear", "穿戴" },
            { "RemoveApparel", "脱下服装" },
            { "Equip", "装备" },
            { "Reload", "装填" },
            { "Sow", "播种" },
            { "Harvest", "收割" },
            { "CutPlant", "砍伐植物" },
            { "HarvestDesignated", "收割指定植物" },
            { "ExtinguishSelf", "自我灭火" },
            { "BeatFire", "扑灭火焰" },
            { "TendPatient", "照顾病人" },
            { "Rescue", "救援" },
            { "Capture", "捕获" },
            { "CarryToCryptosleepCasket", "搬运到冬眠舱" },
            { "TakeToBed", "搬到床上" },
            { "BuryCorpse", "埋葬尸体" },
            { "Open", "打开" },
            { "EnterCryptosleepCasket", "进入冬眠舱" },
            { "UseNeurotrainer", "使用神经训练器" },
            { "UseArtifact", "使用神器" },
            { "Kidnap", "绑架" },
            { "Steal", "偷窃" },
            { "Flick", "切换开关" },
            { "EnterTransporter", "进入运输舱" },
            { "LoadTransporters", "装载运输舱" },
            { "RefuelAtomic", "原子加油" },
            { "Refuel", "加油" },
            { "RearmTrap", "重置陷阱" },
            { "Slaughter", "屠宰" },
            { "Train", "训练" },
            { "Milk", "挤奶" },
            { "Shear", "剪毛" },
            { "Tame", "驯服" },
            { "ReleaseAnimals", "释放动物" },
            { "RopeToPen", "用绳子牵到围栏" },
            { "Nuzzle", "亲近" },
            { "Mate", "交配" },
            { "LayEgg", "产蛋" },
            
            // 战斗
            { "AttackMelee", "近战攻击" },
            { "AttackStatic", "静态攻击" },
            { "Flee", "逃跑" },
            { "FleeAndCower", "逃跑并蜷缩" },
            { "Hunt", "狩猎" },
            { "PredatorHunt", "捕食者狩猎" },
            { "ManTurret", "操作炮塔" },
            
            // 特殊
            { "UseCommsConsole", "使用通讯控制台" },
            { "TradeWithPawn", "与人交易" },
            { "ViewArt", "欣赏艺术" },
            { "PlayBilliards", "玩台球" },
            { "PlayChess", "下棋" },
            { "PlayHorseshoes", "玩马蹄铁游戏" },
            { "PlayMusicalInstrument", "演奏乐器" },
            { "ListenToMusic", "听音乐" },
            { "WatchTelevision", "看电视" },
            { "Skygaze", "观星" },
            { "Vomit", "呕吐" },
            { "PrepareSkylantern", "准备天灯" },
            { "ReleaseSkylantern", "放飞天灯" },
            { "BuildSnowman", "堆雪人" },
            { "HoldingPlatformEffigy", "建造神像" },
            { "StudyBuilding", "研究建筑" },
            { "StudyThing", "研究物品" },
            { "UseWorkbench", "使用工作台" },
            
            // 囚犯相关
            { "PrisonerAttemptRecruit", "尝试招募囚犯" },
            { "PrisonerFriendlyChat", "与囚犯友好交谈" },
            { "PrisonerExecution", "处决囚犯" },
            { "ReleasePrisoner", "释放囚犯" },
            { "EscortPrisonerToBed", "护送囚犯到床位" },
            { "Arrest", "逮捕" },
            
            // 仪式
            { "VisitGrave", "祭扫坟墓" },
            { "Scarify", "圣疤仪式" },
            { "GetBlinding", "致盲仪式" },
            { "BestowingCeremony", "授勋仪式" },
            
            // 心理和情绪
            { "Breakdown", "精神崩溃" },
            { "Tantrum", "发脾气" },
            { "Binging", "暴饮暴食" },
            { "InsultingSpree", "连续侮辱" },
            { "RunWild", "狂野奔跑" },
            { "Wander", "漫步" },
            
            // 交通
            { "EnterShuttle", "登上穿梭机" },
            { "ExitShuttle", "离开穿梭机" },
            { "BoardShuttle", "登机" },
            
            // 动物
            { "Ingest_Animal", "动物进食" },
            { "Ingest_AnimalHunt", "动物狩猎进食" },
        };
        
        public override Vector2 InitialSize => new Vector2(750f, 650f);
        
        public Window_JobTriggerSettings()
        {
            this.doCloseButton = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;
            this.closeOnClickedOutside = false;
        }
        
        public override void PreOpen()
        {
            base.PreOpen();
            InitializeJobList();
        }
        
        private void InitializeJobList()
        {
            try
            {
                allJobs = DefDatabase<JobDef>.AllDefsListForReading
                    .Where(j => j != null)
                    .OrderBy(j => j.defName)
                    .ToList();
                
                CategorizeJobs();
                ApplyFilter();
                initialized = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] 加载 JobDef 列表失败: {ex.Message}");
                allJobs = new List<JobDef>();
                filteredJobs = new List<JobDef>();
                categorizedJobs = new Dictionary<string, List<JobDef>>();
            }
        }
        
        private void CategorizeJobs()
        {
            categorizedJobs = new Dictionary<string, List<JobDef>>();
            foreach (var category in CategoryNames.Keys)
            {
                categorizedJobs[category] = new List<JobDef>();
            }
            
            foreach (var job in allJobs)
            {
                string category = DetermineJobCategory(job);
                categorizedJobs[category].Add(job);
                categorizedJobs["all"].Add(job);
            }
        }
        
        private string DetermineJobCategory(JobDef job)
        {
            string defName = job.defName;
            
            foreach (var kvp in CategoryKeywords)
            {
                foreach (var keyword in kvp.Value)
                {
                    if (defName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return kvp.Key;
                    }
                }
            }
            
            return "other";
        }
        
        private void ApplyFilter()
        {
            if (allJobs == null)
            {
                filteredJobs = new List<JobDef>();
                return;
            }
            
            var settings = RimTalkExpandActionsMod.Settings;
            IEnumerable<JobDef> source = selectedCategory == "all" 
                ? allJobs 
                : (categorizedJobs.ContainsKey(selectedCategory) ? categorizedJobs[selectedCategory] : allJobs);
            
            // 应用搜索过滤
            if (!string.IsNullOrEmpty(searchFilter))
            {
                string lowerFilter = searchFilter.ToLower();
                source = source.Where(j => 
                    j.defName.ToLower().Contains(lowerFilter) ||
                    (j.label != null && j.label.ToLower().Contains(lowerFilter)));
            }
            
            // 只显示已启用的
            if (showEnabledOnly && settings != null)
            {
                source = source.Where(j => settings.enabledJobTriggers.Contains(j.defName));
            }
            
            filteredJobs = source.ToList();
        }
        
        public override void DoWindowContents(Rect inRect)
        {
            try
            {
                var settings = RimTalkExpandActionsMod.Settings;
                if (settings == null)
                {
                    Widgets.Label(inRect, "错误: 设置未初始化");
                    return;
                }
                
                float y = 0f;
                float width = inRect.width;
                
                // === 标题区域 ===
                DrawTitle(ref y, width);
                
                // === 搜索和筛选区域 ===
                DrawSearchAndFilter(ref y, width, settings);
                
                // === 分类选择 ===
                DrawCategoryTabs(ref y, width);
                
                // === 快捷操作按钮 ===
                DrawQuickActions(ref y, width, settings);
                
                // === 统计信息 ===
                DrawStatistics(ref y, width, settings);
                
                // === 分隔线 ===
                y += GAP;
                Widgets.DrawLineHorizontal(0f, y, width);
                y += GAP;
                
                // === Job 列表 ===
                DrawJobList(ref y, width, inRect.height, settings);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] Job 配置窗口错误: {ex}");
            }
        }
        
        private void DrawTitle(ref float y, float width)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, width, 30f), "Job 触发器配置");
            Text.Font = GameFont.Small;
            y += 32f;
            
            // 说明文字
            GUI.color = Color.gray;
            Widgets.Label(new Rect(0f, y, width, LINE_HEIGHT), 
                "选择当小人开始以下 Job 时自动触发 RimTalk 对话");
            GUI.color = Color.white;
            y += LINE_HEIGHT + GAP;
        }
        
        private void DrawSearchAndFilter(ref float y, float width, RimTalkExpandActionsSettings settings)
        {
            // 搜索框
            Widgets.Label(new Rect(0f, y, 60f, LINE_HEIGHT), "搜索:");
            
            Rect searchRect = new Rect(65f, y, width - 220f, LINE_HEIGHT);
            string newFilter = Widgets.TextField(searchRect, searchFilter ?? "");
            if (newFilter != searchFilter)
            {
                searchFilter = newFilter;
                ApplyFilter();
            }
            
            // 清除搜索按钮
            if (!string.IsNullOrEmpty(searchFilter))
            {
                Rect clearRect = new Rect(searchRect.xMax + 5f, y, 60f, LINE_HEIGHT);
                if (Widgets.ButtonText(clearRect, "清除"))
                {
                    searchFilter = "";
                    ApplyFilter();
                }
            }
            
            // 只显示已启用复选框
            Rect enabledOnlyRect = new Rect(width - 140f, y, 140f, LINE_HEIGHT);
            bool wasShowEnabled = showEnabledOnly;
            Widgets.CheckboxLabeled(enabledOnlyRect, "只显示已启用", ref showEnabledOnly);
            if (wasShowEnabled != showEnabledOnly)
            {
                ApplyFilter();
            }
            
            y += LINE_HEIGHT + GAP;
        }
        
        private void DrawCategoryTabs(ref float y, float width)
        {
            float tabWidth = (width - (CategoryNames.Count - 1) * 4f) / CategoryNames.Count;
            float x = 0f;
            
            foreach (var kvp in CategoryNames)
            {
                Rect tabRect = new Rect(x, y, tabWidth, BUTTON_HEIGHT);
                
                bool isSelected = selectedCategory == kvp.Key;
                if (isSelected)
                {
                    Widgets.DrawBoxSolid(tabRect, new Color(0.2f, 0.4f, 0.6f, 0.5f));
                }
                
                // 显示分类和数量
                int count = categorizedJobs != null && categorizedJobs.ContainsKey(kvp.Key) 
                    ? categorizedJobs[kvp.Key].Count 
                    : 0;
                string label = $"{kvp.Value} ({count})";
                
                if (Widgets.ButtonText(tabRect, label, true, true, isSelected ? Color.white : Color.gray))
                {
                    selectedCategory = kvp.Key;
                    ApplyFilter();
                }
                
                x += tabWidth + 4f;
            }
            
            y += BUTTON_HEIGHT + GAP;
        }
        
        private void DrawQuickActions(ref float y, float width, RimTalkExpandActionsSettings settings)
        {
            float buttonWidth = (width - 16f) / 5f;
            float x = 0f;
            
            // 全选当前
            if (Widgets.ButtonText(new Rect(x, y, buttonWidth, BUTTON_HEIGHT), "全选当前"))
            {
                SelectAllFiltered(settings);
            }
            x += buttonWidth + 4f;
            
            // 取消全选
            if (Widgets.ButtonText(new Rect(x, y, buttonWidth, BUTTON_HEIGHT), "取消当前"))
            {
                DeselectAllFiltered(settings);
            }
            x += buttonWidth + 4f;
            
            // 反选
            if (Widgets.ButtonText(new Rect(x, y, buttonWidth, BUTTON_HEIGHT), "反选当前"))
            {
                InvertFiltered(settings);
            }
            x += buttonWidth + 4f;
            
            // 清除所有
            if (Widgets.ButtonText(new Rect(x, y, buttonWidth, BUTTON_HEIGHT), "清除所有"))
            {
                settings.enabledJobTriggers.Clear();
            }
            x += buttonWidth + 4f;
            
            // 推荐预设
            if (Widgets.ButtonText(new Rect(x, y, buttonWidth, BUTTON_HEIGHT), "推荐预设"))
            {
                ApplyRecommendedPreset(settings);
            }
            
            y += BUTTON_HEIGHT + GAP;
        }
        
        private void SelectAllFiltered(RimTalkExpandActionsSettings settings)
        {
            if (filteredJobs == null) return;
            
            foreach (var job in filteredJobs)
            {
                if (!settings.enabledJobTriggers.Contains(job.defName))
                {
                    settings.enabledJobTriggers.Add(job.defName);
                }
            }
        }
        
        private void DeselectAllFiltered(RimTalkExpandActionsSettings settings)
        {
            if (filteredJobs == null) return;
            
            foreach (var job in filteredJobs)
            {
                settings.enabledJobTriggers.Remove(job.defName);
            }
        }
        
        private void InvertFiltered(RimTalkExpandActionsSettings settings)
        {
            if (filteredJobs == null) return;
            
            foreach (var job in filteredJobs)
            {
                if (settings.enabledJobTriggers.Contains(job.defName))
                {
                    settings.enabledJobTriggers.Remove(job.defName);
                }
                else
                {
                    settings.enabledJobTriggers.Add(job.defName);
                }
            }
        }
        
        private void DrawStatistics(ref float y, float width, RimTalkExpandActionsSettings settings)
        {
            int enabledCount = settings.enabledJobTriggers?.Count ?? 0;
            int filteredCount = filteredJobs?.Count ?? 0;
            int totalCount = allJobs?.Count ?? 0;
            
            // 进度条样式的统计
            Rect statRect = new Rect(0f, y, width, LINE_HEIGHT);
            
            // 背景条
            Widgets.DrawBoxSolid(new Rect(0f, y + 8f, width, 8f), new Color(0.2f, 0.2f, 0.2f, 0.5f));
            
            // 已启用比例
            if (totalCount > 0)
            {
                float ratio = (float)enabledCount / totalCount;
                Widgets.DrawBoxSolid(new Rect(0f, y + 8f, width * ratio, 8f), new Color(0.3f, 0.6f, 0.3f, 0.7f));
            }
            
            // 统计文字
            string statText = $"已启用: {enabledCount} / {totalCount} | 当前显示: {filteredCount}";
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(statRect, statText);
            Text.Anchor = TextAnchor.UpperLeft;
            
            y += LINE_HEIGHT + GAP;
        }
        
        private void DrawJobList(ref float y, float width, float totalHeight, RimTalkExpandActionsSettings settings)
        {
            float listHeight = totalHeight - y - 10f;
            Rect listOuterRect = new Rect(0f, y, width, listHeight);
            
            if (!initialized)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(listOuterRect, "正在加载 Job 列表...");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            
            if (filteredJobs == null || filteredJobs.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(listOuterRect, showEnabledOnly ? "没有已启用的 Job" : "未找到匹配的 Job");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            
            // 计算视图高度
            float viewHeight = filteredJobs.Count * ROW_HEIGHT;
            Rect viewRect = new Rect(0f, 0f, listOuterRect.width - 16f, viewHeight);
            
            Widgets.BeginScrollView(listOuterRect, ref scrollPosition, viewRect);
            
            float listY = 0f;
            for (int i = 0; i < filteredJobs.Count; i++)
            {
                var job = filteredJobs[i];
                if (job == null) continue;
                
                DrawJobRow(job, i, viewRect.width, ref listY, settings);
            }
            
            Widgets.EndScrollView();
        }
        
        private void DrawJobRow(JobDef job, int index, float width, ref float y, RimTalkExpandActionsSettings settings)
        {
            Rect rowRect = new Rect(0f, y, width, ROW_HEIGHT - 2f);
            
            // 交替背景色
            if (index % 2 == 1)
            {
                Widgets.DrawBoxSolid(rowRect, new Color(0.1f, 0.1f, 0.1f, 0.3f));
            }
            
            // 鼠标悬停高亮
            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawBoxSolid(rowRect, new Color(0.3f, 0.3f, 0.5f, 0.2f));
                
                // 工具提示
                string tooltip = BuildJobTooltip(job);
                TooltipHandler.TipRegion(rowRect, tooltip);
            }
            
            bool isEnabled = settings.enabledJobTriggers.Contains(job.defName);
            bool wasEnabled = isEnabled;
            
            // 复选框
            Rect checkboxRect = new Rect(4f, y + 1f, CHECKBOX_SIZE, CHECKBOX_SIZE);
            Widgets.Checkbox(checkboxRect.position, ref isEnabled);
            
            // Job 名称
            float labelX = checkboxRect.xMax + 8f;
            Rect labelRect = new Rect(labelX, y, width - labelX - 80f, ROW_HEIGHT - 2f);
            
            // 构建标签
            string label = job.defName;
            if (!string.IsNullOrEmpty(job.label))
            {
                label += $" <color=#888888>({job.label})</color>";
            }
            
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 分类标签
            string category = DetermineJobCategory(job);
            if (category != "other" && CategoryNames.ContainsKey(category))
            {
                Rect categoryRect = new Rect(width - 70f, y + 2f, 65f, ROW_HEIGHT - 4f);
                GUI.color = GetCategoryColor(category);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(categoryRect, CategoryNames[category]);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            
            // 更新状态
            if (isEnabled != wasEnabled)
            {
                if (isEnabled)
                    settings.enabledJobTriggers.Add(job.defName);
                else
                    settings.enabledJobTriggers.Remove(job.defName);
            }
            
            y += ROW_HEIGHT;
        }
        
        private string BuildJobTooltip(JobDef job)
        {
            string tooltip = $"<b>{job.defName}</b>";
            
            // 添加中文翻译
            string chineseTranslation = GetChineseTranslation(job.defName);
            if (!string.IsNullOrEmpty(chineseTranslation))
            {
                tooltip += $"\n<color=#FFDD00>【中文】{chineseTranslation}</color>";
            }
            
            if (!string.IsNullOrEmpty(job.label))
            {
                tooltip += $"\n游戏标签: {job.label}";
            }
            
            if (!string.IsNullOrEmpty(job.description))
            {
                tooltip += $"\n\n{job.description}";
            }
            
            string category = DetermineJobCategory(job);
            if (CategoryNames.ContainsKey(category))
            {
                tooltip += $"\n\n分类: {CategoryNames[category]}";
            }
            
            return tooltip;
        }
        
        /// <summary>
        /// 获取 Job 的中文翻译
        /// </summary>
        private string GetChineseTranslation(string defName)
        {
            // 首先尝试精确匹配
            if (JobTranslations.TryGetValue(defName, out string translation))
            {
                return translation;
            }
            
            // 尝试部分匹配（按关键词）
            foreach (var kvp in JobTranslations)
            {
                if (defName.Contains(kvp.Key) || kvp.Key.Contains(defName))
                {
                    return kvp.Value + " (推测)";
                }
            }
            
            // 尝试智能翻译常见词根
            return TranslateByKeywords(defName);
        }
        
        /// <summary>
        /// 根据关键词智能翻译
        /// </summary>
        private string TranslateByKeywords(string defName)
        {
            var translations = new List<string>();
            
            // 常见词根翻译
            var keywords = new Dictionary<string, string>
            {
                { "Wait", "等待" },
                { "Goto", "前往" },
                { "Follow", "跟随" },
                { "Attack", "攻击" },
                { "Hunt", "狩猎" },
                { "Haul", "搬运" },
                { "Build", "建造" },
                { "Construct", "建造" },
                { "Repair", "修理" },
                { "Clean", "清洁" },
                { "Mine", "采矿" },
                { "Research", "研究" },
                { "Craft", "制作" },
                { "Cook", "烹饪" },
                { "Sow", "播种" },
                { "Harvest", "收割" },
                { "Cut", "砍伐" },
                { "Tend", "照料" },
                { "Feed", "喂食" },
                { "Rescue", "救援" },
                { "Capture", "捕获" },
                { "Train", "训练" },
                { "Tame", "驯服" },
                { "Milk", "挤奶" },
                { "Shear", "剪毛" },
                { "Slaughter", "屠宰" },
                { "Butcher", "屠宰" },
                { "Play", "玩耍" },
                { "Watch", "观看" },
                { "Listen", "聆听" },
                { "Social", "社交" },
                { "Chat", "聊天" },
                { "Talk", "交谈" },
                { "Romance", "浪漫" },
                { "Lovin", "亲热" },
                { "Marry", "结婚" },
                { "Prisoner", "囚犯" },
                { "Recruit", "招募" },
                { "Execute", "处决" },
                { "Arrest", "逮捕" },
                { "Flee", "逃跑" },
                { "Melee", "近战" },
                { "Shoot", "射击" },
                { "Range", "远程" },
                { "Turret", "炮塔" },
                { "Ingest", "进食" },
                { "Eat", "吃" },
                { "Drink", "喝" },
                { "LayDown", "躺下" },
                { "Sleep", "睡觉" },
                { "Rest", "休息" },
                { "Meditate", "冥想" },
                { "Pray", "祈祷" },
                { "Ritual", "仪式" },
                { "Ceremony", "典礼" },
                { "Visit", "访问" },
                { "Grave", "坟墓" },
                { "Bury", "埋葬" },
                { "Corpse", "尸体" },
                { "Strip", "剥夺" },
                { "Wear", "穿戴" },
                { "Equip", "装备" },
                { "Reload", "装填" },
                { "Refuel", "加油" },
                { "Flick", "切换" },
                { "Open", "打开" },
                { "Enter", "进入" },
                { "Exit", "离开" },
                { "Load", "装载" },
                { "Unload", "卸载" },
                { "Install", "安装" },
                { "Deconstruct", "解构" },
                { "Remove", "移除" },
                { "Floor", "地板" },
                { "Roof", "屋顶" },
                { "Smooth", "平整" },
                { "Art", "艺术" },
                { "Music", "音乐" },
                { "Television", "电视" },
                { "Chess", "象棋" },
                { "Billiards", "台球" },
                { "Breakdown", "崩溃" },
                { "Tantrum", "发怒" },
                { "Wander", "徘徊" },
                { "Wild", "狂野" },
                { "Animal", "动物" },
                { "Mate", "交配" },
                { "Egg", "蛋" },
                { "Nuzzle", "亲近" },
                { "Study", "研究" },
                { "Use", "使用" },
                { "Trade", "交易" },
                { "Shuttle", "穿梭机" },
                { "Transporter", "运输舱" },
            };
            
            foreach (var kvp in keywords)
            {
                if (defName.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    translations.Add(kvp.Value);
                }
            }
            
            if (translations.Count > 0)
            {
                return string.Join("/", translations.Distinct()) + " (推测)";
            }
            
            return null;
        }
        
        private Color GetCategoryColor(string category)
        {
            switch (category)
            {
                case "social": return new Color(0.5f, 0.8f, 0.5f);
                case "work": return new Color(0.8f, 0.7f, 0.4f);
                case "combat": return new Color(0.9f, 0.4f, 0.4f);
                case "movement": return new Color(0.4f, 0.7f, 0.9f);
                case "basic": return new Color(0.7f, 0.7f, 0.9f);
                default: return Color.gray;
            }
        }
        
        private void ApplyRecommendedPreset(RimTalkExpandActionsSettings settings)
        {
            // 推荐的社交触发 Job
            var recommended = new List<string>
            {
                "SocialRelax",
                "StandAndBeSociallyActive",
                "Ingest",
                "LayDown",
                "Wait_MaintainPosture",
                "Meditate",
                "Research",
                "DoBill",
                "Wait_Wander",
                "Lovin",
            };
            
            settings.enabledJobTriggers.Clear();
            foreach (var jobName in recommended)
            {
                if (allJobs != null && allJobs.Any(j => j.defName == jobName))
                {
                    settings.enabledJobTriggers.Add(jobName);
                }
            }
            
            Messages.Message($"已应用推荐预设，共启用 {settings.enabledJobTriggers.Count} 个 Job", 
                MessageTypeDefOf.PositiveEvent, false);
        }
    }
}