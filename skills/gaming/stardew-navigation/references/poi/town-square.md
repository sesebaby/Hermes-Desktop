# 小镇广场 POI

当意图是“去小镇”“去广场”“去喷泉”“去公共场合露面”或去中心社交集合点时，使用本 POI。

## 机械目标

`target(locationName=Town,x=42,y=17,source=map-skill:stardew.navigation.poi.town-square)`

## 使用

- 适合公共等待、社交露面和随意观察小镇。
- 本地 executor 读取到这个 target 后，调用 `stardew_navigate_to_tile` 执行；之后用 `stardew_task_status` 查看进度。
