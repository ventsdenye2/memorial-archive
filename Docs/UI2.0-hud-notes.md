# UI2.0 HUD 与剧情页接入核对

## 范围与映射

- 本批处理 `GameplayHUD.prefab`、`OpeningDialoguePanel.prefab` 及其必要运行时脚本；教程和 `BlackScreenStoryPanel` 只做宿主核对，不猜测未确认的步骤映射。
- 明确映射是：`游戏页面` 用于 HUD；本轮用户明确指定 `自白页` 用于 `OpeningStory` 的对白面板。HUD 原有自言自语通路没有明确发布者，本批不新增、不套用自白页。
- `OpeningStory.unity` 中实际播放入口是名为 `OpeningStoryFlowController` 的 `DialogueSceneController`（`dialogueId: opening_dialogue`），它打开 `PanelId.Dialogue` 并使用 `OpeningDialoguePanel/DialoguePresentationView`。因此本批将自白页的框与继续提示落到 `OpeningDialoguePanel`，保留 CG、肖像、音效、打字机和转场绑定。
- `剧情页`、`对话页` 以及 `教程页1/2/3` 当前没有可由代码和配置共同确认的运行时宿主或步骤映射，记录为未决，不替换到 `OpeningStory` 或 `GuideOverlayPanel`。

## 迁移入口

在 Unity 菜单执行：`Memorial Archive > UI > Apply UI2.0 HUD and Opening Dialogue`。

入口脚本是 `Assets/Editor/UI2HudMigration.cs`，操作可重复执行，保存两个 prefab 和脚本序列化引用，但不主动调用 `AssetDatabase.Refresh()`。执行后建议让主 agent 在 Unity 中检查 Console 与 Prefab Inspector。

## GameplayHUD

- 保留 `HUDPanel`、`HUDButtonActions`、`GuideHudVisibility`、PanelId 和现有导航按钮事件；导航按钮仍分别调用 `OpenSystem`、`OpenInventory`、`OpenDiary`、`OpenMap`。
- 生命值、能量条、快捷栏和副手格按 `游戏页面/定位参考.png` 的原生比例布局。生命值与能量条仍由 `HUDPanel` 的运行时填充逻辑驱动。
- 快捷栏继续保留三格加副手格的绑定；`HUDPanel` 新增 `offhandSlotSprite`，空手/副手格使用 `装备格子（副手）.png`，选中快捷格仍使用选中框。
- `TaskPanel` 是定位图中可确认的装饰和占位内容。当前仓库没有发现任务数据视图或任务栏展开事件的明确绑定，因此保留 `任务一` 与定位图示例数字作为静态占位，并在有真实任务数据承载后再接入。
- HUD 不在本批创建 `NarrationPanel` 或 HUD `DialoguePresentationView`。当前仓库没有发现明确的 HUD 自白发布者，因此不能凭空把自白页接入 HUD；若后续确认该宿主和发布事件，再单独接入。

## OpeningDialoguePanel

- `DialoguePanel` 的全屏暗色 Image 改为透明，底部加入 `自白页/话框.png`，对白文字放入框内，并使用 `自白页/按任意键继续.png`。
- 保留 CG、左右肖像槽、闪屏、音效、打字机和 PanelId/暂停行为；没有替换 `opening_dialogue` 当前 CG 或肖像配置。
- `剧情页` 的 CG/剧情框、`对话页` 角色图/普通对话框和 `教程页1/2/3` 全屏图没有明确宿主，本批只记录映射，不套到 OpeningStory 或 GuideOverlayPanel。
- 迁移入口会遍历两个 prefab 的 UI Graphic，显式补齐 `CanvasRenderer`，避免手工 prefab 导入时反复出现 Creating missing CanvasRenderer（包括 DialoguePanel、CG、DialogueText、FlashOverlay、两侧肖像与 dim overlay）。

## 字体与验收限制

- UI2.0 目录中的 `字体标准.png` 是定位截图，不是可导入字体；公共字体压缩包实际只有 txt，兰梓目录没有可用 ttf/otf。本次使用仓库已有 `Assets/Font/simhei.ttf` 作为可读回退，不下载或安装未知字体。
- 验收入口：执行 Unity 菜单迁移后，检查两个 prefab 的子节点、图片引用、`HUDPanel` 数组引用和 CanvasRenderer；进入 `OpeningStory`，确认 `opening_dialogue` 的旁白与角色节点都显示自白框、打字机和继续提示，剧情结束仍由原 `DialogueSceneController` 转场。HUD 本批只验收游戏页面布局与导航绑定，不宣称 HUD 自白通路已接入。
