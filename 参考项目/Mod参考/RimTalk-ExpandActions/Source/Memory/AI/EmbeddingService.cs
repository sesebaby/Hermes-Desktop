using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace RimTalkExpandActions.Memory.AI
{
    /// <summary>
    /// 嵌入向量服务（调用 SiliconFlow API）
    /// 使用 UnityWebRequest 进行异步 HTTP 请求
    /// 
    /// v2.2 新增：支持自定义向量模型和 API URL
    /// </summary>
    public static class EmbeddingService
    {
        // 默认配置（可被 Settings 覆盖)
        private const string DEFAULT_API_URL = "https://api.siliconflow.cn/v1/embeddings";
        private const string DEFAULT_MODEL_NAME = "BAAI/bge-large-zh-v1.5";

        /// <summary>
        /// 获取文本嵌入向量（异步）
        /// </summary>
        /// <param name="text">要处理的文本</param>
        /// <param name="onSuccess">成功回调（参数为向量数组）</param>
        /// <param name="onFailure">失败回调（参数为错误消息）</param>
        public static void GetEmbedding(string text, Action<float[]> onSuccess, Action<string> onFailure)
        {
            try
            {
                // 基础验证
                if (string.IsNullOrWhiteSpace(text))
                {
                    onFailure?.Invoke("输入文本为空");
                    return;
                }

                // 获取 API Key
                string apiKey = RimTalkExpandActionsMod.Settings?.SiliconFlowApiKey;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    onFailure?.Invoke("未配置 SiliconFlow API Key，请在 Mod 设置中填写");
                    return;
                }

                // 获取自定义配置
                string apiUrl = RimTalkExpandActionsMod.Settings?.GetEmbeddingApiUrl() ?? DEFAULT_API_URL;
                string modelName = RimTalkExpandActionsMod.Settings?.GetEmbeddingModel() ?? DEFAULT_MODEL_NAME;

                // 使用 CoroutineRunner 启动协程
                Coroutine coroutine = CoroutineRunner.Run(GetEmbeddingCoroutine(text, apiKey, apiUrl, modelName, onSuccess, onFailure));
                
                if (coroutine == null)
                {
                    string error = "CoroutineRunner 启动失败";
                    Log.Error($"[RimTalk-ExpandActions] {error}");
                    onFailure?.Invoke(error);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] EmbeddingService.GetEmbedding 失败: {ex.Message}\n{ex.StackTrace}");
                onFailure?.Invoke($"向量请求失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 协程：执行 API 请求
        /// </summary>
        private static IEnumerator GetEmbeddingCoroutine(string text, string apiKey, string apiUrl, string modelName, Action<float[]> onSuccess, Action<string> onFailure)
        {
            // 构建 JSON 请求体
            string jsonBody = BuildJsonRequest(text, modelName);

            // 创建 UnityWebRequest
            UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
            
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            
            // 设置请求头
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            // 发送请求
            yield return request.SendWebRequest();

            // 处理响应 (兼容旧版 Unity，不使用 request.result)
            if (!request.isNetworkError && !request.isHttpError)
            {
                try
                {
                    string responseText = request.downloadHandler.text;

                    // 手动解析 JSON（避免 JsonUtility 限制）
                    float[] embedding = ParseEmbeddingFromJson(responseText);

                    if (embedding != null && embedding.Length > 0)
                    {
                        onSuccess?.Invoke(embedding);
                    }
                    else
                    {
                        string error = "无法从响应中提取向量数据";
                        Log.Error($"[RimTalk-ExpandActions] EmbeddingService: {error}");
                        onFailure?.Invoke(error);
                    }
                }
                catch (Exception ex)
                {
                    string error = $"解析 API 响应失败: {ex.Message}";
                    Log.Error($"[RimTalk-ExpandActions] EmbeddingService: {error}\n{ex.StackTrace}");
                    onFailure?.Invoke(error);
                }
            }
            else
            {
                string error = $"API 请求失败: {request.error} (Code: {request.responseCode})";
                Log.Error($"[RimTalk-ExpandActions] EmbeddingService: {error}");
                onFailure?.Invoke(error);
            }

            request.Dispose();
        }

        /// <summary>
        /// 手动解析 JSON 响应，提取 embedding 数组
        /// 格式: {"object":"list","data":[{"object":"embedding","embedding":[0.1,0.2,...],"index":0}],...}
        /// </summary>
        private static float[] ParseEmbeddingFromJson(string json)
        {
            try
            {
                // 1. 找到 "data" 字段的位置
                int dataIndex = json.IndexOf("\"data\"");
                if (dataIndex < 0)
                {
                    Log.Error("[RimTalk-ExpandActions] JSON 中未找到 'data' 字段");
                    return null;
                }

                // 2. 找到 data 数组的开始位置 [
                int arrayStart = json.IndexOf('[', dataIndex);
                if (arrayStart < 0)
                {
                    Log.Error("[RimTalk-ExpandActions] 未找到 data 数组的开始符号");
                    return null;
                }

                // 3. 找到第一个 "embedding" 字段
                int embeddingIndex = json.IndexOf("\"embedding\"", arrayStart);
                if (embeddingIndex < 0)
                {
                    Log.Error("[RimTalk-ExpandActions] JSON 中未找到 'embedding' 字段");
                    return null;
                }

                // 4. 找到 embedding 数组的开始位置 [
                int embArrayStart = json.IndexOf('[', embeddingIndex);
                if (embArrayStart < 0)
                {
                    Log.Error("[RimTalk-ExpandActions] 未找到 embedding 数组的开始符号");
                    return null;
                }

                // 5. 找到 embedding 数组的结束位置 ]
                int embArrayEnd = json.IndexOf(']', embArrayStart);
                if (embArrayEnd < 0)
                {
                    Log.Error("[RimTalk-ExpandActions] 未找到 embedding 数组的结束符号");
                    return null;
                }

                // 6. 提取数组内容
                string arrayContent = json.Substring(embArrayStart + 1, embArrayEnd - embArrayStart - 1);
                
                // 7. 分割并转为 float 数组
                string[] parts = arrayContent.Split(',');
                float[] embedding = new float[parts.Length];

                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i].Trim();
                    if (!float.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out embedding[i]))
                    {
                        embedding[i] = 0f;
                    }
                }

                return embedding;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] ParseEmbeddingFromJson 失败: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 构建 JSON 请求体（手动拼接，避免使用 Newtonsoft.Json）
        /// v2.2: 支持自定义模型名称
        /// </summary>
        private static string BuildJsonRequest(string text, string modelName)
        {
            // 转义特殊字符
            string escapedText = text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");

            return "{\"model\":\"" + modelName + "\",\"input\":\"" + escapedText + "\"}";
        }
    }
}
