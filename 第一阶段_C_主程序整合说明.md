# 第一阶段 C：主程序整合说明

## 场景流程

`MainMenu.unity` 点击开始后加载 `SampleScene.unity`；黑屏开场文本使用故事 `storyId = 1`，结束后展示 Stage1DemoRoot。场景内 `GameRoot`、玩家、相机跟随、交互点、UI 注册表均由第一阶段基线生成器配置。

## C 负责的接入点

- `InventorySystem` 订阅容器打开、容器关闭、快捷键与道具使用事件。
- `InteractionSystem` 对 `stage1_container_01`、`stage1_container_02` 发布容器打开事件；UI 同时打开背包、容器、快捷栏相关面板。
- `SavePanel` 发布存档请求，`SaveManager` 调用各存档模块；`InventorySystem` 以模块键 `inventory` 保存与恢复物品状态。
- `Stage1DemoSceneValidator` 在 SampleScene 启动时验证关键场景根节点、唯一玩家和相机、交互 ID、出生点、6 个道具配置与两组容器初始内容。

## 合并前检查

1. 在 Unity 中打开 SampleScene，Console 不应出现编译错误。
2. 运行 `Stage1DemoSceneValidator/Validate Stage 1 Scene`，日志应出现 `scene contract passed`。
3. 完整走一遍：主菜单 → 黑屏开场 → 场景 → 容器拾取 → 存档。
