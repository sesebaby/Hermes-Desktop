using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimTalkExpandActions.Memory.Utils
{
    /// <summary>
    /// 线程安全的 Pawn 缓存系统
    /// 定期在主线程更新缓存，避免在后台线程访问 map.mapPawns
    /// </summary>
    public class ThreadSafePawnCache
    {
        private static ThreadSafePawnCache instance;
        private Dictionary<string, Pawn> pawnCache = new Dictionary<string, Pawn>();
        private int lastUpdateTick = -1;
        private const int CacheRefreshInterval = 60; // 每60 tick 更新一次

        public static ThreadSafePawnCache Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ThreadSafePawnCache();
                }
                return instance;
            }
        }

        /// <summary>
        /// 在主线程更新缓存（由 GameComponent 定期调用）
        /// </summary>
        public void UpdateCache()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (currentTick - lastUpdateTick < CacheRefreshInterval)
            {
                return;
            }

            try
            {
                var newCache = new Dictionary<string, Pawn>();

                if (Find.Maps != null)
                {
                    foreach (var map in Find.Maps)
                    {
                        if (map?.mapPawns == null) continue;

                        foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                        {
                            if (pawn == null || pawn.Name == null) continue;

                            // 使用短名字作为键
                            string shortName = pawn.Name.ToStringShort.ToLower().Replace(" ", "");
                            if (!newCache.ContainsKey(shortName))
                            {
                                newCache[shortName] = pawn;
                            }

                            // 使用昵称作为键
                            if (pawn.Name is NameTriple nameTriple && !string.IsNullOrEmpty(nameTriple.Nick))
                            {
                                string nickname = nameTriple.Nick.ToLower().Replace(" ", "");
                                if (!newCache.ContainsKey(nickname))
                                {
                                    newCache[nickname] = pawn;
                                }
                            }
                        }
                    }
                }

                // 原子替换缓存
                pawnCache = newCache;
                lastUpdateTick = currentTick;

                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message($"[ThreadSafePawnCache] 缓存已更新，共 {pawnCache.Count} 个条目");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ThreadSafePawnCache] 更新缓存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 线程安全地查找 Pawn（使用缓存）
        /// </summary>
        public Pawn FindPawnByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string normalizedName = name.ToLower().Replace(" ", "");

            // 直接查找
            if (pawnCache.TryGetValue(normalizedName, out Pawn exactMatch))
            {
                return exactMatch;
            }

            // 模糊查找
            foreach (var kvp in pawnCache)
            {
                if (kvp.Key.Contains(normalizedName) || normalizedName.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// 根据名字列表查找多个 Pawn
        /// </summary>
        public List<Pawn> FindPawnsByNames(string names)
        {
            var result = new List<Pawn>();

            if (string.IsNullOrWhiteSpace(names))
            {
                return result;
            }

            // 分割名字（支持逗号、分号、中文逗号）
            string[] nameArray = names.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string name in nameArray)
            {
                string trimmedName = name.Trim();
                if (string.IsNullOrEmpty(trimmedName))
                {
                    continue;
                }

                Pawn pawn = FindPawnByName(trimmedName);
                if (pawn != null && !result.Contains(pawn))
                {
                    result.Add(pawn);
                }
                else if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Warning($"[ThreadSafePawnCache] 未找到 Pawn: {trimmedName}");
                }
            }

            return result;
        }

        /// <summary>
        /// 清空缓存
        /// </summary>
        public void ClearCache()
        {
            pawnCache.Clear();
            lastUpdateTick = -1;
        }

        /// <summary>
        /// 获取缓存大小
        /// </summary>
        public int GetCacheSize()
        {
            return pawnCache.Count;
        }
    }
}
