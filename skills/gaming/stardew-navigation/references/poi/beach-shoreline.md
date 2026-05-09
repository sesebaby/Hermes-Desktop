# 海边海岸线 POI

当意图是“去海边”“去沙滩”“到海边走走”“去海岸线”“找一个阳光好的拍照点”或安静的水边目的地时，使用本 POI。

## 机械目标

`target(locationName=Beach,x=32,y=34,source=map-skill:stardew.navigation.poi.beach-shoreline)`

## 使用

- 适合海莉想去明亮、漂亮、开阔的地方。
- 本地 executor 读取到这个 target 后，调用 `stardew_navigate_to_tile` 执行；之后用 `stardew_task_status` 查看进度。
