# 场景物品摆放核对（2026-09-13）

按用户最新说明跳过档案室 B、档案室 C、厕所。检查其余 11 个运行场景；
FrontHall 包含已合并的一层走廊，不重复检查 Floor_1F 制作源场景。
本次只检查、截图和记录，没有移动或保存场景物品。

## 明确的承托问题：6 处

坐标为当前场景世界坐标，截图位于项目 Logs/PlacementAudit。

| 场景 / 交互 ID | 坐标 (x,y) | 当前问题 | 建议调整 |
| --- | --- | --- | --- |
| Floor_4F / Floor_4F_save | 9.65,-2.6 | 电话悬在高书柜右侧，底下没有桌面 | 放到现有矮柜台面；四层精确目标位置待参考图 |
| Room_Office / Room_Office_diary_2 | 5.6,-2.8 | 平铺纸条浮在书柜前，未贴合层板 | 放到桌面，或明确对齐书柜层板 |
| Room_ArchiveA / Room_ArchiveA_records | -10.7,-2.8 | 纸条水平悬在倾斜书柜前，角度及落点不贴合 | 优先放到参考图左侧书桌的纸条位置 |
| Room_ArchiveA / Room_ArchiveA_alice | 0.4,-2.8 | 纸条悬在椅背前方，高于座面 | 放到桌面或合理地面位置 |
| Room_TreatmentA / Room_TreatmentA_blood_note | 5.6,-2.8 | 纸条悬在输液架与倾斜床架之间 | 移到有承托的台面或地面 |
| Room_TreatmentB / Room_TreatmentB_note_1 | -13.2,-3 | 纸条浮在左侧屏风中部 | 按参考图移到最右侧地面纸条位置 |

对应截图文件名为 `<场景>_<交互ID>.png`。

## 特殊灯具的安装位置：6 处需调整

普通壁灯整体贴合墙面；以下独立乌鸦灯的视觉安装关系不合理。
建议移动到清晰墙面，并同步交互位置和光源配置，不仅移动贴图。

| 场景 / 灯 ID | 坐标 (x,y) | 问题 |
| --- | --- | --- |
| Room_Office / light_room_office_special | 15.6,-1.4 | 灯具压在窗口和窗帘上 |
| Room_Director / light_room_director_special | 16.3,-1.4 | 灯具挂在窗帘前，缺少墙面安装关系 |
| Room_Reception / light_room_reception_special | 9.1,-1.4 | 灯具位于椅背上方窗帘前 |
| Room_TreatmentA / light_room_treatmenta_special | 14.1,-1.4 | 灯具与墙上药品架重叠 |
| Room_TreatmentB / light_room_treatmentb_special | 10.5,-1.4 | 灯具压在屏风布面上 |
| Floor_4F / light_floor_4f_special | 18,-1.4 | 灯具遮住画框，应错开 |

档案室 A 乌鸦灯位于墙上，但与旁边普通壁灯距离过近，有局部重叠，建议一并错开。

## 各场景结果

| 场景 | 核对结果 |
| --- | --- |
| FrontHall（含一层走廊） | 电话在柜面；三张纸条分别有桌面、展台、柜面支撑；特殊灯在两窗之间墙上。一层走廊未见同类悬空物品 |
| Floor_2F | 壁灯与特殊灯贴墙；走廊陈设未见同类承托问题 |
| Floor_3F | 电话位于台灯旁的柜面，显示由静态美术层承担，交互点无独立 Sprite 不构成缺失 |
| Floor_4F | 电话悬空、特殊灯遮画框；未提供四层参考图，不能确认整体还原程度 |
| Room_Office | 电话、主纸条落在书桌上；新增日记和特殊灯见问题表 |
| Room_ArchiveA | 电话在柜面；两张纸条见问题表；特殊灯需错开普通壁灯 |
| Room_Director | 电话、左桌纸条及沙发座面纸条有支撑；特殊灯见问题表 |
| Room_Reception | 电话在柜面；特殊灯见问题表。参考图纸条未对应当前剧情内容，不据此新增文案；Cecil 演出立绘不按落地道具判错 |
| Room_TreatmentA | 主纸条在矮柜台面；新增血迹纸条及特殊灯见问题表 |
| Room_TreatmentB | 电话在左侧矮柜上；纸条位置与参考图右侧地面不符；特殊灯见问题表 |
| Room_Terrace | 纸条在地面；特殊灯附着右侧柱体，承托和安装关系合理 |

三层新增前景柱属于用户明确要求，即使提供的三层合成参考图未画柱子，也不作为差异错误。

## 检查方法与证据边界

- 使用 `Tools/UpdatedCorridors/placement_audit.py` 单场景打开、相机渲染全景及交互点细节，结束恢复编辑器场景。
- 有效数据：`Logs/PlacementAudit/points.json`。仅计入 `isActiveAndEnabled` 的交互组件，排除旧停用根对象。
- 11 个场景共 201 个启用交互点：144 灯、22 出口、13 纸条、9 容器、8 存档点、4 躲藏点、1 NPC。
- 未发现同场景重复启用交互 ID；201 个触发区均覆盖步行高度 y=-5.2。此项是几何检查，不等于逐点 Play Mode 功能测试。
- 画面为编辑器直接相机渲染，不包含运行时黑暗遮罩、动态动画和完整 UI；本次不声称已完成运行时交互验收。
- 旧截图中存在停用对象的历史文件；以 points.json 内有效 ID 和本报告列出的截图为准。
