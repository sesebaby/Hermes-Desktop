# 星露谷导航索引

本文件只用于选择下一步读取哪个区域参考文件。它故意不包含机械坐标。

## 区域

- 海边 / 沙滩 / 海岸 / 码头 / 潮池 / 南侧出口：读取 `references/regions/beach.md`。
- 小镇 / 广场 / 喷泉 / 商店 / 诊所道路 / 公共集合点：读取 `references/regions/town.md`。

## 规则

不要从本索引直接输出 `target(locationName,x,y,source)`。必须先读取相关 region 文件，再读取 POI 文件。
