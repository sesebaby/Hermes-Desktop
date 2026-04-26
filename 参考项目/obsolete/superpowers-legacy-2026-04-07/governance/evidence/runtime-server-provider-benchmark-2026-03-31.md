# Runtime + Server Provider Benchmark

状态：

- active benchmark note

日期：

- 2026-03-31

用途：

- 记录 `Runtime.Local -> Cloud Control -> provider -> hosted create/finalize` 链路下三种 provider/model 的真实基准结果
- 证明这次结果不是 `.temp` 直连 DashScope 文本脚本，而是当前仓库 `runtime + server` 实际链路

范围：

- `private_dialogue`
- `npc.haley`
- `runtime + cloud-control` 受控基准

基准方法：

- provider 通过 `Cloud Control` 的 `/operator-console/api/provider` 切换
- 每轮请求统一打：
  - `/runtime/stardew/private-dialogue`
  - 成功后再打 `/runtime/stardew/private-dialogue/{canonicalRecordId}/finalize`
- 三组 provider/model：
  - `aliyun-dashscope / qwen-plus-character`
  - `aliyun-dashscope-flash / qwen3.5-flash`
  - `aliyun-deepseek-v3-2 / deepseek-v3.2`
- 每组 `15` 次

结果文件：

- `artifacts/benchmarks/runtime-server-provider-benchmark-fixed-20260331-175121.json`

## Summary

| Provider | Model | Success | Avg ms | Median ms | Min ms | Max ms | Finalize avg ms | Failure reasons |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| `aliyun-dashscope` | `qwen-plus-character` | `14/15` | `1331.33` | `1298.06` | `1070.15` | `1603.97` | `140.04` | `hosted_narrative_invalid_response x1` |
| `aliyun-dashscope-flash` | `qwen3.5-flash` | `15/15` | `1617.51` | `1649.77` | `1065.53` | `2133.03` | `137.29` | `none` |
| `aliyun-deepseek-v3-2` | `deepseek-v3.2` | `15/15` | `2567.98` | `2540.48` | `1564.90` | `3631.89` | `136.28` | `none` |

## Interpretation

- 当前三条 provider/model 在修复后都可用。
- 当前受控链路里的速度排序是：
  1. `qwen-plus-character`
  2. `qwen3.5-flash`
  3. `deepseek-v3.2`
- `qwen3.5-flash` 在本次修复前曾出现：
  - `provider_timeout`
  - `provider_invalid_response`
  - `hosted_narrative_unavailable`
- 最终确认这些都不是模型本身问题，而是当前仓库链路中的：
  - provider `sidecar: null` 归一化缺失
  - internal token header 含 BOM / 非 ASCII 字符

## Repair Notes

- `ProviderJsonResponseNormalizer` 现在会把 provider action 的 `sidecar: null` 归一化为空对象，而不是直接 fail-closed。
- `InternalServiceAuth.ResolveToken(...)` 现在会清洗 BOM 和首尾空白，避免 service-to-service header 在本地 HTTP 栈里直接炸掉。
- 这次 benchmark 保持了你要求的模型集合，不再通过切换默认模型来“绕过问题”。
