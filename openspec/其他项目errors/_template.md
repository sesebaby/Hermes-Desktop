# E-YYYY-NNNN-short-slug

- id: E-YYYY-NNNN
- title:
- status: active
- updated_at: YYYY-MM-DD
- keywords: [keyword1, keyword2, keyword3]
- trigger_scope: [proposal, design, tasks, implementation, review]

## Symptoms

- 用户、reviewer 或后续 implementation AI 看见了什么异常现象？
- 哪些关键约束在文档里看起来存在，但实施时仍要临场猜？

## Root Cause

- 根因不是“谁写错了一行”，而是哪个治理判断缺位？
- 哪些 authority source、contract、carrier、owner、reason code 没被提前冻结？

## Bad Fix Paths

- 哪些修法会继续把问题推迟到 implementation AI？
- 哪些“先跑起来再说”的路线必须禁止复发？

## Corrective Constraints

- Constraint 1
- Constraint 2
- Constraint 3

## Verification Evidence

- 哪些文档、spec、task 结构、review 记录或手动验证说明能证明旧错误没再发生？
- 哪个 reason code、字段表、入口表、承载面说明证明主链已冻结？

## Related Files

- openspec/project.md
- openspec/changes/<change-name>/proposal.md
- openspec/changes/<change-name>/design.md
- openspec/changes/<change-name>/tasks.md

## Notes

- 如已命中通用 `afw-unified-governance` error 卡，在这里写明对应通用卡名称与本仓库特化点。
