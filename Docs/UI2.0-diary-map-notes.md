# UI2.0 日记与地图接入记录

## 已核对素材

- `笔记/定位参考图.png` 与 `地图/定位页.png` 均为 1920×1080 定位图，仅用于核对布局，不作为运行时底图。
- `笔记/笔记本背景.png` 为 797×1019 的半透明书本合成层，已经包含 Diary Book 标题、No.210、页面装饰、页码和关闭图形；`笔记/透明度蒙版 拷贝 4.png` 为 1920×1080 的黑色 80% 蒙版。
- 日记页签使用 `日记.png` / `日记（选中）.png`，纸条页签使用 `纸条.png` / `纸条（选中）.png`；翻页按钮使用 `前一页.png` 与 `后一页.png`，保留原有 `Toggle`、`Button` 和层级。
- `地图/地图弹窗背景.png` 是不透明的 1920×1080 完整地图合成层，已经包含纸张、建筑线稿、房间文字、入口和右上角“你的位置”图例；关闭按钮使用 `地图/退出键.png` 与 `退出键 （选中）.png`。

## 迁移与行为

`UI2DiaryMapMigration.Apply()` 位于 `Assets/Editor/UI2DiaryMapMigration.cs`，入口为 Unity 菜单 `Tools/Memorial Archive/Apply UI2.0 Diary and Map`。

- DiaryPanel 保留 `DiaryPanel`、`CanvasGroup`、`CommonPanelActions.CloseOwner`、两个页签、左右翻页按钮以及禁用态子对象；只替换 Image 素材和定位尺寸。Diary 页签作为默认选中态，纸条页签为未选中态，和定位图一致。
- MapPanel 使用完整地图合成层铺满面板，隐藏旧的占位标题和关闭按钮文字；关闭按钮仍调用原 `CommonPanelActions.CloseOwner`。
- UI2.0 只提供了 `实时位置.png`；本次根据 `D:/MagnetNeedle/需求/场景/场景/scene_layout.png` 与仓库内 `Assets/Art/Scenes/场景地图1草图.png`，为现有同名场景写入了可审查的归一化坐标。坐标属于 Inspector 配置，不由运行时根据房间名称猜测。
- `MapPanel` 订阅 `SceneLoadedEvent`、`RoomEnteredEvent` 和 `RoomExitedEvent`。已配置 `Floor_1F`、`Floor_2F`、`Floor_3F`、`Floor_4F`、`FrontHall`、`Room_Director`、`Room_Office`、`Room_ArchiveA/B/C`、`Room_TreatmentA/B`、`Room_Reception` 和 `Room_Toilet`；`旧档案室`、`禁闭室` 没有同名场景，保留未映射。没有命中配置时动态标记隐藏，底图右上角的“你的位置”仅为图例。
- 坐标按 UI2.0 1920×1080 底图归一化（原点在左下）：楼梯层级为 `Floor_4F=(0.424,0.570)`、`Floor_3F=(0.424,0.450)`、`Floor_2F=(0.424,0.360)`、`Floor_1F=(0.591,0.250)`；前厅为 `FrontHall=(0.464,0.250)`。房间点位与底图中对应格子的中心对齐，完整值保存在 `MapPanel.prefab` 的 `markerPlacements`。

## 字体与待核对项

- `字体标准.png` 是定位说明图，字体包目录只有 txt 说明，没有可导入字体文件；本次未声称接入新字体，运行时仍使用工程现有字体回退。
- 书页正文没有对应的运行时数据或 UI 字段，迁移不伪造正文内容；正文区域由后续日记数据接入决定。

## 验收入口

在 Unity 执行 `Tools/Memorial Archive/Apply UI2.0 Diary and Map`，然后打开一个包含 HUD 的场景：按 `I` 打开日记，检查蒙版、书本、页签、左右翻页按钮与关闭按钮；按 `M` 打开地图，检查 16:9 底图、房间标注、入口/“你的位置”图例与关闭按钮。确认按钮 hover/pressed 时没有旧占位色块，且 `PanelId.Diary` / `PanelId.Map` 的开关逻辑保持可用。
