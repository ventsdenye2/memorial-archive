# 场景美术来源核对（2026-09-13）

第一阶段结论：**尚未全部符合 Imported / 8.4 来源要求。**

核对了 17 个场景：15 个游戏场景（包含已停用的 Floor_1F 制作源场景）、
MainMenu、OpeningStory。读取实际 SpriteRenderer、UI Image 和 MeshRenderer 的
引用，并分别记录启用状态。当前启用的场景 SpriteRenderer 共 120 个，引用
76 个不同纹理；其中下列 3 个背景不符合路径要求。

| 场景 | 当前使用的背景 |
| --- | --- |
| Room_ArchiveB | Assets/Art/Scenes/档案室Bd.png |
| Room_ArchiveC | Assets/Art/Scenes/档案室Cd.png |
| Room_Toilet | Assets/Art/Scenes/厕所D.jpg |

这三处均位于 SceneLayout0909/Art/Background。在 Imported 或 8.4 目录中
未找到对应的档案室 B/C、厕所新版候选素材，不能通过迁移目录冒充新版来源。
其余已启用的场景 SpriteRenderer 的底层纹理均符合指定来源。
合并前厅接缝柱的 Sprite 资产虽在 Assets/Art 根目录，其底层纹理实际来自
Imported/UpdatedCorridors/1F/Source/组 6 副本.png，因此来源符合要求。

此外，场景中还引用了以下其他类别资源，不能据此声称所有美术引用都符合：

| 类别 | 来源 |
| --- | --- |
| 背包蒙版 | Assets/Art/UI/背包页/蒙版图层(背包页）.png |
| 系统蒙版 | Assets/Art/UI/系统页/蒙版图层.png |
| 玩家 Spine 纹理 | Assets/Actions/Player01/Idle/player_02_Idle_split.png |
| Cecil Spine 纹理 | Assets/Art/Characters/Cecil/npc_01.png |
| 通用 UI 底板 | Unity 内置 Resources/unity_builtin_extra |

以上为场景现有组件引用清单，不代表已经穷举所有运行时动态加载的图标或特效。

用户已说明档案室 B/C、厕所新素材尚未到位，授权先检查其余场景摆放。
第二阶段已完成其余 11 个游戏场景的编辑器画面与交互组件核对，见
[PLACEMENT_AUDIT.md](PLACEMENT_AUDIT.md)。FrontHall 已包含一层走廊。
本次未改动场景、美术引用或物品位置。四层尚缺用户指定参考图，
仅判断现有画面的承托、遮挡关系。玩家动画和 UI 来源例外独立记录。

完整机器清单：Logs/ArtAudit/references.json。
来源例外：Logs/ArtAudit/outside-approved-folders.json。
复核入口：Tools/UpdatedCorridors/art_audit.py（只读）。
