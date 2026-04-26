using System;
using System.Collections;
using UnityEngine;
using Verse;

namespace RimTalkExpandActions.Memory.AI
{
    /// <summary>
    /// 协程运行器（用于在非 MonoBehaviour 类中运行协程）
    /// </summary>
    public class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;
        private static bool _isQuitting = false;

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static CoroutineRunner Instance
        {
            get
            {
                if (_isQuitting)
                {
                    if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                    {
                        Log.Warning("[RimTalk-ExpandActions] CoroutineRunner: 应用程序正在退出，跳过初始化");
                    }
                    return null;
                }

                if (_instance == null)
                {
                    try
                    {
                        // 创建 GameObject 并添加 CoroutineRunner 组件
                        GameObject go = new GameObject("RimTalkExpandActions_CoroutineRunner");
                        
                        // 确保 GameObject 处于激活状态
                        go.SetActive(true);
                        
                        _instance = go.AddComponent<CoroutineRunner>();
                        
                        // 防止场景切换时销毁
                        DontDestroyOnLoad(go);
                        
                        if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                        {
                            Log.Message("[RimTalk-ExpandActions] CoroutineRunner 已初始化并激活");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[RimTalk-ExpandActions] CoroutineRunner 初始化失败: {ex.Message}\n{ex.StackTrace}");
                        return null;
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 启动协程的便捷方法
        /// </summary>
        public static Coroutine Run(IEnumerator coroutine)
        {
            try
            {
                if (coroutine == null)
                {
                    if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                    {
                        Log.Warning("[RimTalk-ExpandActions] CoroutineRunner.Run: 协程为 null");
                    }
                    return null;
                }

                if (Instance == null)
                {
                    Log.Error("[RimTalk-ExpandActions] CoroutineRunner.Run: Instance 为 null，无法启动协程");
                    return null;
                }

                return Instance.StartCoroutine(coroutine);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] CoroutineRunner.Run 失败: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 应用程序退出时的清理
        /// </summary>
        private void OnApplicationQuit()
        {
            _isQuitting = true;
            
            if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
            {
                Log.Message("[RimTalk-ExpandActions] CoroutineRunner: 应用程序正在退出");
            }
        }

        /// <summary>
        /// 组件销毁时的清理
        /// </summary>
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                
                if (RimTalkExpandActionsMod.Settings?.enableDetailedLogging == true)
                {
                    Log.Message("[RimTalk-ExpandActions] CoroutineRunner 已销毁");
                }
            }
        }
    }
}
