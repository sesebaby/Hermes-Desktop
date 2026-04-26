# Superpowers Specs Index

## 1. 文档角色

本文只做：

1. `系统设计目录入口`
2. `阅读顺序导航`
3. `目录入口说明`

本文不新增设计真相，也不登记第二套附件或 contract 清单。  
当前唯一设计真相只来自：

1. `docs/superpowers/governance/current-phase-boundary.md`
2. `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
3. 主文第 `13` 节登记的正式附件
4. 主文和正式附件回链到的 `contracts / profiles / governance / evidence`

## 2. 固定阅读顺序

1. `docs/superpowers/governance/current-phase-boundary.md`
2. 本文
3. `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`
4. 主文第 `13` 节登记的正式附件
5. 对应 `contracts / profiles / governance / evidence`

## 3. 正式入口

### 3.1 总主文

- `docs/superpowers/specs/2026-03-27-superpowers-master-design.md`

### 3.2 正式附件注册表入口

- 只认主文第 `13` 节的正式附件注册表
- `README` 不再重复抄一遍附件名单

### 3.3 契约与治理目录入口

1. `docs/superpowers/contracts/product/`
2. `docs/superpowers/contracts/runtime/`
3. `docs/superpowers/profiles/`
4. `docs/superpowers/governance/`
5. `docs/superpowers/evidence/`

## 4. 目录治理规则

1. `attachments/` 目录只允许保留当前正式附件，或明确标记为 `retired reference` 的辅助参考。
2. 未被主文第 `13` 节登记的附件，不得再被当成当前 authority。
3. 历史稿统一转入：
   - `docs/superpowers/specs/obsolete/`
4. `profiles` 里的旧附件引用，必须标成：
   - `辅助参考`
   不能继续标成：
   - `上位依赖`

## 5. 一句话规则

1. `README` 只导航，不立新规。
2. 正式附件只认主文第 `13` 节。
3. 旧稿和旧方法只允许作为 `retired reference` 或退役专表存在。
