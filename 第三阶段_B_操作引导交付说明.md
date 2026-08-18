# 第三阶段 B 操作引导交付说明

交付人：程序员 B
交付分支：`feature/stage3-guide-system`（基于 `develop` daab953）
需求依据：`doc/玩家引导系统.md` 第 2 章「操作引导」+ 第 2.2 系统流程；`doc/第三阶段_A_B_D_分工.md`
验收日期：2026-08-04

## 一、本次交付范围

按分工文档，B 负责「通用引导框架 + 操作引导」。本轮交付：

1. 配置驱动的通用 `GuideSystem`（顺序步骤、并行步骤组、延时显示、自动隐藏、真实条件完成、必做步骤、已完成不重复、进度存档/新游戏重置）。
2. 操作引导用例：进入前厅后依次非强制引导移动 / 背包 / 装备手提灯 / 奔跑 / 体力。
3. 通用 `GuideOverlayPanel` 非模态覆盖层（不暂停游戏、不阻塞移动）。
4. 发布 `GuideStepStartedEvent` / `GuideStepCompletedEvent` / `GuideStepHiddenEvent`，View 只根据事件显示，不自行推进步骤。
5. 一次性生成器 `Stage3GuideBootstrap`（产出配置资产与预制体）。

**不做**（按分工）：光照系统（A）、UI 引导（D，本轮鸽）、战斗引导（鸽）。

## 二、实际修改文件清单

### 新增（Guide 系统，均带 .meta）

| 文件 | 层 | 说明 |
|---|---|---|
| `Assets/Scripts/Gameplay/Guide/Config/GuideStepConfig.cs` | Config | 单步 ScriptableObject 配置 |
| `Assets/Scripts/Gameplay/Guide/Config/GuideSequenceConfig.cs` | Config | 序列配置（含 triggerSceneId） |
| `Assets/Scripts/Gameplay/Guide/Data/GuideData.cs` | Data | `[Serializable] GuideSaveData`（已完成步骤 id） |
| `Assets/Scripts/Gameplay/Guide/Logic/GuideSystem.cs` | Logic | 引导系统核心：`IGameSystem, ITickableSystem, ISaveModule, INewGameResettable` |
| `Assets/Scripts/Gameplay/Guide/View/GuideOverlayPanel.cs` | View | 非模态引导覆盖层，订阅事件显示 |
| `Assets/Editor/Stage3GuideBootstrap.cs` | Editor | 一次性生成器（菜单 `Tools/Memorial Archive/Build Stage 3 Guide`） |
| `Assets/GameConfigs/Guide/*.asset` | 资产 | 操作引导序列 + 5 步配置 |
| `Assets/Prefabs/UI/GuideOverlayPanel.prefab` | 资产 | 引导覆盖层预制体 |

### 公共文件增量改动（仅必要接入，未重构他人系统）

| 文件 | 改动 |
|---|---|
| `Framework/Event/GameEvents.cs` | 文件末尾追加 3 个事件 struct：`GuideStepStartedEvent` / `GuideStepCompletedEvent` / `GuideStepHiddenEvent` |
| `Framework/UI/PanelId.cs` | 枚举加 `GuideOverlay` |
| `Framework/UI/UIManager.cs` | 新增私有 `IsNonModalOverlay(PanelId)`；在 `IsGameplayInputBlocked` / `Open` / `OpenDirect` / `CloseForExclusiveOpen` / `ApplyPauseState` 五处用其判断，使 `GuideOverlay` 与 `Hud` 同等（不入栈、不暂停、不阻塞输入、不被顶掉） |
| `Framework/Config/GameConfigDatabase.cs` | 加 `GuideSequenceConfig[] guideSequences` 字段与 getter + using |
| `Framework/Config/ConfigManager.cs` | 加 `guideSequences` 字典、Clear、`AddAll`、`GetGuideSequence(id)` + using |
| `Framework/Core/GameRoot.cs` | `RegisterSystem(new GuideSystem());` + using |
| `Assets/GameConfigs/GameConfigDatabase.asset` | `guideSequences` 数组引用「操作引导」序列 |

## 三、依赖但未修改的公共接口

- `EventBus`（订阅/发布）、`IGameSystem` / `ITickableSystem` / `ISaveModule` / `INewGameResettable`：直接实现，未改。
- `SaveManager`：通过 `ISaveModule` 自动注册（`ModuleKey="guide"`），未改。
- `BasePanel`：`GuideOverlayPanel` 继承，未改。

## 四、验证（Unity Console 0 Error）

- 头less 编译检查（`Unity -batchmode -quit`）：通过，无 `error CS`，无 `Scripts have compiler errors`。
- `Stage3GuideBootstrap.BuildStage3Guide` 执行：成功，输出 `Stage 3 guide sequence, step configs and overlay prefab built successfully.`，产出全部配置资产与预制体。

> 运行时逐项验收（对照需求第 2.1 八步 + 2.2 流程）需由 C 在 FrontHall 场景接入预制体后进行，见第六节接入步骤。

## 五、复现步骤

1. 切到 `feature/stage3-guide-system`，Unity 打开工程。
2.（资产已随分支提交）如需重建：菜单 `Tools/Memorial Archive/Build Stage 3 Guide`。
3. 由 C 把 `Assets/Prefabs/UI/GuideOverlayPanel.prefab` 实例放入 FrontHall 场景的持久 Canvas（或通过 `ScenePanelRegistry` 注册），`panelId` 已设为 `GuideOverlay`。
4. 从主菜单点「新游戏」→ 开场剧情 → 进入 FrontHall。
5. 预期：进前厅约 1 秒后同时弹出「移动」「背包」提示；玩家移动后 3 秒消失；打开背包并装备手提灯后该步完成；弹出「奔跑」提示 3 秒消失；触发奔跑后弹出「体力」提示 3 秒消失。

## 六、C 整合接入步骤（场景挂接）

1. 打开 `Assets/Scenes/FrontHall.unity`。
2. 把 `GuideOverlayPanel.prefab` 实例化到场景持久 Canvas 下（与 HUD 同级）。
3. 确认其 `GuideOverlayPanel` 组件：`panelId=GuideOverlay`、`pausesGame=false`、`startClosed=true`，`entryPrefab`/`entryContainer` 已在预制体内连好。
4. 面板实例必须加入该场景 `ScenePanelRegistry.panels`（UIManager 只打开已注册的面板，不注册则 Open 只会刷 `Panel not registered: GuideOverlay` 警告、UI 不显示）。FrontHall 已接入并注册。它走非模态特例，Open 时不入模态栈。
5. GameRoot 已自动注册 `GuideSystem`，无需额外接线。

## 七、已知问题（按类型分类）

### 配置
- 手提灯（1006）当前 `canEquipToShortcut=0`、只能装备副手。需求文档写「拖拽至快捷栏」。**引导逻辑按「装备即完成」判定**（监听 `CharacterEquipmentChangedEvent.OffhandType==Lantern`），不绑定快捷栏/副手，对两种配置都成立。槽位归属若策划确认为快捷栏，需 D/主程序改 1006 配置（B 不越权改物品配置）。
- 引导提示图为占位（`sprite` 留空）。美术资产交付后，在对应 `GuideStepConfig.asset` 的 `sprite` 字段填入即可。

### 场景挂接
- `GuideOverlayPanel.prefab` 已生成但**未放入 FrontHall 场景**，需 C 按第六节接入后才能在运行时显示。未接入前引导逻辑仍会正常推进（事件照常发布），只是看不到 UI。

### 核心代码
- 无已知问题。完成条件一律判断真实结果（移动输入/奔跑/背包打开/装备事件），不依赖按钮点击。
- 笔记/地图图标隐藏：已实现。`GuideSystem` 在序列开始/结束时发布 `GuideSequenceActiveChangedEvent`，`GameplayHUD.prefab` 根节点挂了 `GuideHudVisibility` 组件订阅该事件，引导期间隐藏 `DiaryButton` / `MapButton`，未改 HUD 面板逻辑。

### 视觉占位
- 提示条目用 Unity 内置 `LegacyRuntime.ttf` + 默认 Image 占位，无美术样式。

## 八、Git 提交边界说明

工作区存在不属于本次任务的未跟踪变更（`Packages/packages-lock.json`、`ProjectSettings/*`、`Assets/Art/CG/*.meta` 等为 Unity 自动导入副作用或先前遗留）。提交时只 `git add` 下列范围，**不使用 `git add -A`**：

- `Assets/Scripts/Gameplay/Guide/**`（含 .meta）
- `Assets/Editor/Stage3GuideBootstrap.cs`（含 .meta）
- `Assets/GameConfigs/Guide/**`（含 .meta）
- `Assets/GameConfigs/GameConfigDatabase.asset`
- `Assets/Prefabs/UI/GuideOverlayPanel.prefab`（含 .meta）
- `Framework` 公共脚本改动：`GameRoot.cs`、`GameEvents.cs`、`PanelId.cs`、`UIManager.cs`、`GameConfigDatabase.cs`、`ConfigManager.cs`（含对应 .meta）
- 本交付说明
