using System;
using Verse;

namespace RimTalkExpandActions.Memory.AI
{
    /// <summary>
    /// 向量计算工具类
    /// 提供余弦相似度等数学运算
    /// </summary>
    public static class VectorUtils
    {
        /// <summary>
        /// 计算两个向量的余弦相似度
        /// </summary>
        /// <param name="vectorA">向量A</param>
        /// <param name="vectorB">向量B</param>
        /// <returns>余弦相似度值，范围 [-1, 1]，返回 0 表示计算失败</returns>
        public static float CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            try
            {
                // 验证输入
                if (vectorA == null || vectorB == null)
                {
                    Log.Error("[RimTalk-ExpandActions] CosineSimilarity: 输入向量为 null");
                    return 0f;
                }

                if (vectorA.Length != vectorB.Length)
                {
                    Log.Error($"[RimTalk-ExpandActions] CosineSimilarity: 向量维度不匹配 ({vectorA.Length} != {vectorB.Length})");
                    return 0f;
                }

                if (vectorA.Length == 0)
                {
                    Log.Error("[RimTalk-ExpandActions] CosineSimilarity: 向量长度为 0");
                    return 0f;
                }

                // 计算点积和模长
                float dotProduct = 0f;
                float magnitudeA = 0f;
                float magnitudeB = 0f;

                for (int i = 0; i < vectorA.Length; i++)
                {
                    dotProduct += vectorA[i] * vectorB[i];
                    magnitudeA += vectorA[i] * vectorA[i];
                    magnitudeB += vectorB[i] * vectorB[i];
                }

                magnitudeA = (float)Math.Sqrt(magnitudeA);
                magnitudeB = (float)Math.Sqrt(magnitudeB);

                // 处理零向量情况
                if (magnitudeA < 1e-10f || magnitudeB < 1e-10f)
                {
                    Log.Warning("[RimTalk-ExpandActions] CosineSimilarity: 检测到零向量，无法计算相似度");
                    return 0f;
                }

                // 计算余弦相似度
                float similarity = dotProduct / (magnitudeA * magnitudeB);

                // 限制范围在 [-1, 1]（由于浮点误差可能略微超出）
                similarity = Math.Max(-1f, Math.Min(1f, similarity));

                return similarity;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk-ExpandActions] CosineSimilarity 计算失败: {ex.Message}");
                return 0f;
            }
        }
    }
}
