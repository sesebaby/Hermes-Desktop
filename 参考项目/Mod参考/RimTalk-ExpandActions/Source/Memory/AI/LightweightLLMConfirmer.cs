using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Verse;

namespace RimTalkExpandActions.Memory.AI
{
    /// <summary>
    /// 轻量LLM确认器
    /// 用于高风险+不确定时的二次确认
    /// 使用约50 tokens的轻量级API调用
    /// </summary>
    public static class LightweightLLMConfirmer
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        
        /// <summary>
        /// 确认结果
        /// </summary>
        public class ConfirmResult
        {
            public bool Success { get; set; }
            public bool Confirmed { get; set; }  // LLM是否确认执行
            public float Confidence { get; set; }
            public string Reason { get; set; }
            public int TokensUsed { get; set; }
            public string Error { get; set; }
        }
        
        /// <summary>
        /// 向轻量LLM确认是否执行行为
        /// </summary>
        /// <param name="intentId">意图ID</param>
        /// <param name="intentName">意图名称</param>
        /// <param name="aiResponse">AI原始回复</param>
        /// <param name="confidence">本地NLU置信度</param>
        /// <returns>确认结果</returns>
        public static async Task<ConfirmResult> ConfirmAsync(
            string intentId,
            string intentName,
            string aiResponse,
            float confidence)
        {
            var settings = RimTalkExpandActionsMod.Settings;
            
            // 检查是否启用
            if (settings == null || !settings.enableLightweightLLM)
            {
                return new ConfirmResult
                {
                    Success = false,
                    Error = "轻量LLM确认未启用"
                };
            }
            
            // 检查API Key
            if (string.IsNullOrWhiteSpace(settings.lightweightLLMApiKey))
            {
                return new ConfirmResult
                {
                    Success = false,
                    Error = "未配置API Key"
                };
            }
            
            try
            {
                // 构建精简的确认请求
                string truncatedResponse = aiResponse.Length > 100 
                    ? aiResponse.Substring(0, 100) 
                    : aiResponse;
                
                // 转义JSON特殊字符
                truncatedResponse = truncatedResponse.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
                string escapedIntentName = intentName.Replace("\\", "\\\\").Replace("\"", "\\\"");
                
                string prompt = "判断回复是否表达\\\"" + escapedIntentName + "\\\"的意图。回复:「" + truncatedResponse + "」只回答Y或N";
                
                string model = string.IsNullOrWhiteSpace(settings.lightweightLLMModel) ? "gpt-4o-mini" : settings.lightweightLLMModel;
                string apiUrl = string.IsNullOrWhiteSpace(settings.lightweightLLMApiUrl) ? "https://api.openai.com/v1/chat/completions" : settings.lightweightLLMApiUrl;
                
                string requestBodyJson = "{" +
                    "\"model\":\"" + model + "\"," +
                    "\"messages\":[{\"role\":\"user\",\"content\":\"" + prompt + "\"}]," +
                    "\"max_tokens\":5," +
                    "\"temperature\":0.1" +
                    "}";
                
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                request.Headers.Add("Authorization", "Bearer " + settings.lightweightLLMApiKey);
                request.Content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.SendAsync(request);
                var responseText = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    return new ConfirmResult
                    {
                        Success = false,
                        Error = "API请求失败: " + response.StatusCode
                    };
                }
                
                // 简单解析JSON响应
                string content = ExtractContent(responseText);
                int tokensUsed = ExtractTokensUsed(responseText);
                
                bool confirmed = content != null && content.Trim().ToUpper().StartsWith("Y");
                
                if (settings.enableDetailedLogging)
                {
                    Log.Message("[LightLLM] ────────────────────────────────");
                    Log.Message("[LightLLM] 意图: " + intentName);
                    Log.Message("[LightLLM] 本地置信度: " + confidence.ToString("F2"));
                    Log.Message("[LightLLM] LLM回复: " + content);
                    Log.Message("[LightLLM] 确认结果: " + (confirmed ? "✓ 确认执行" : "✗ 不执行"));
                    Log.Message("[LightLLM] Token消耗: " + tokensUsed);
                    Log.Message("[LightLLM] ────────────────────────────────");
                }
                
                return new ConfirmResult
                {
                    Success = true,
                    Confirmed = confirmed,
                    Confidence = confirmed ? 1.0f : 0f,
                    Reason = confirmed ? "LLM确认执行" : "LLM判断不执行",
                    TokensUsed = tokensUsed
                };
            }
            catch (TaskCanceledException)
            {
                return new ConfirmResult
                {
                    Success = false,
                    Error = "请求超时"
                };
            }
            catch (Exception ex)
            {
                Log.Error("[LightLLM] 确认失败: " + ex.Message);
                return new ConfirmResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }
        
        /// <summary>
        /// 从JSON响应中提取content字段
        /// </summary>
        private static string ExtractContent(string json)
        {
            try
            {
                // 使用正则表达式提取 "content":"..." 的值
                var match = Regex.Match(json, "\"content\"\\s*:\\s*\"([^\"]*(?:\\\\.[^\"]*)*)\"");
                if (match.Success)
                {
                    string content = match.Groups[1].Value;
                    // 反转义
                    content = content.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
                    return content;
                }
            }
            catch { }
            return null;
        }
        
        /// <summary>
        /// 从JSON响应中提取total_tokens字段
        /// </summary>
        private static int ExtractTokensUsed(string json)
        {
            try
            {
                var match = Regex.Match(json, "\"total_tokens\"\\s*:\\s*(\\d+)");
                if (match.Success)
                {
                    return int.Parse(match.Groups[1].Value);
                }
            }
            catch { }
            return 0;
        }
        
        /// <summary>
        /// 同步版本（用于不支持异步的场景）
        /// </summary>
        public static ConfirmResult Confirm(
            string intentId,
            string intentName,
            string aiResponse,
            float confidence)
        {
            try
            {
                return ConfirmAsync(intentId, intentName, aiResponse, confidence).Result;
            }
            catch (AggregateException ae)
            {
                return new ConfirmResult
                {
                    Success = false,
                    Error = ae.InnerException?.Message ?? ae.Message
                };
            }
        }
        
        /// <summary>
        /// 测试API连接
        /// </summary>
        public static async Task<bool> TestConnectionAsync()
        {
            var result = await ConfirmAsync("test", "测试", "这是一个测试", 0.5f);
            return result.Success;
        }
    }
}