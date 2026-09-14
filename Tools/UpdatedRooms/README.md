# 2026-09-14 档案室 B/C、厕所素材更新

素材原件：`Assets/Art/Imported/UpdatedRooms0914`，三份压缩包内共 15 张 PNG，逐文件 SHA-256 与原包一致。定位合成图只作参考，场景使用纸张、背景组及独立道具分层，100 PPU、3840×1080、无拉伸。

本次只更新 `Room_ArchiveB`、`Room_ArchiveC`、`Room_Toilet`。摄像机与碰撞边界统一为 X=-19.2…19.2。保留已有交互 ID、容器内容、剧情、房间连接和玩家/UI。

- B：抽屉柜左上角 (1182,654)，纸条左上角 (2721,733)，按参考图匹配。出口对齐中央关闭门板，移除旧版左侧叠加门图。
- C：补齐独立衣柜，容器交互随衣柜移动；两份剧情纸条落在桌面；右侧出口保留项目已有门素材。出生点置于 X=14，避开灯具、衣柜与出口交互范围。
- 厕所：补齐中部独立隔间，躲藏点对齐关闭隔间，容器对齐洗手台旁柜体，出口放置于最右侧空白墙段。
- 三间房的灯具、交互触发器、出生点及 C 的守卫位置同步调整；旧宽度的边墙不再挡住通路。

## 重应用

已导入的素材不依赖 Downloads。`prepare.py` 同步房间清单、旧场景重建清单、交互清单和剧情坐标。修改后运行 `python -X utf8 Tools/UpdatedRooms/prepare.py`。

退出 Play Mode 并保存已打开的场景后，使用 Unity 菜单 **Tools > Memorial Archive > Apply Updated Archive And Toilet Art**。该工具只修改三间房；不运行旧的全项目重建。原有离线场景重建工具也已支持显式 `spawn_x`，避免重建时恢复 C 的旧出生位置。

## 验证

执行 **Validate Scene Layout** 与 **Validate Interaction Layout**。本次 15 个场景、247 个交互点的检查均无错误，包含引用、配置、出入口、交互范围间距和地面可达性。

Unity 全景与通道碰撞检查：`Logs/UpdatedRooms0914/*-unity.png`、`walking-lane.json`。最终全景仅显示环境，临时隐藏玩家后拍摄，未保存该隐藏状态。

Play Mode 中运行 `python -X utf8 Tools/UpdatedRooms/runtime_qa.py`，检查三个房间的实际入口位置、出生点无交互重叠、左右相机边缘、每个交互触发器覆盖行走高度，以及返回楼层的位置。模拟已完成前厅障碍引导的游戏进度，仅修改本次运行内存，不写存档；退出 Play Mode 即清除。

导入校验、运行结果与截图保存在 `Logs/UpdatedRooms0914`。此次未重新打包可执行文件。
