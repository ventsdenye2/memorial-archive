# 第一阶段 A UI 交付说明

## 已交付内容

- `Assets/Scenes/MainMenu.unity`：启动场景，挂接主菜单和新游戏确认窗口。
- `Assets/Prefabs/UI/MainMenuPanel.prefab`：新游戏、继续游戏、读取存档、设置、退出五个入口。
- `Assets/Prefabs/UI/NewGameConfirmPanel.prefab`：确认与取消按钮。
- `Assets/Prefabs/UI/GameplayHUD.prefab`：生命、体力、提示、系统/背包/日记/地图入口和三格快捷栏外观。
- `Assets/Prefabs/UI/DiaryPanel.prefab`：日记/纸条占位页、翻页外观和关闭按钮。
- `Assets/Art/UI/封面/`：主菜单实际引用的四张按钮图及原始 `.meta`；保留 GUID，避免运行时变成白块。

其余第一阶段 Panel 继续使用 `develop` 上已有 Prefab。`SystemPanel.prefab` 是暂停/系统页的唯一正式资源；不使用 A 分支中命名和职责均错误的 `StopPanel.prefab`。

## 命名修正

- `Dairy` 改为 `DiaryTab`。
- `UnLeftrArrow`、`UnRightArrow` 改为 `LeftArrowDisabled`、`RightArrowDisabled`。
- HUD 的三格快捷栏改为 `ShortcutSlot_1`、`ShortcutSlot_2`、`ShortcutSlot_3`。
- 多余的任务占位按钮和第 4 个快捷栏格保留为禁用占位对象，分别命名为 `TaskPlaceholderButton`、`ExtraShortcutSlot`。
- `StopTitle` 改为 `Subtitle`，`UnHealth1/2/3` 改为 `EmptyHealth_1/2/3`。
- 主菜单原本隐藏且位置异常的 `ContinueButton` 已恢复，并按五按钮顺序重新排布。
- `SystemPanel` 中职责实际为“继续游戏”的 `CloseButton` 改名为 `ContinueButton`。

## 合并边界

- 没有接收 A 分支的 `InventoryPanel.cs`，因为该文件会把 C 已完成的背包逻辑覆盖为空类。
- 没有接收 `StopPanel.prefab`，因为它实际挂的是主菜单控制脚本，按钮职责也不是系统页职责。
- 没有用无共同祖先的分支直接合并整棵工程；只移植了本说明中列出的 UI 资产，保留 `develop` 的公共脚本、配置、Packages、ProjectSettings 和 `SampleScene.unity`。

## 验证路线

1. 从 Build Settings 的第 0 个场景 `MainMenu.unity` 进入。
2. 检查五个主菜单按钮；新游戏可取消，也可确认进入 `SampleScene.unity`。
3. 检查黑屏剧情结束后 HUD 出现。
4. 检查 HUD 的系统、背包、日记、地图按钮与 `ESC/TAB/I/M` 一致。
5. 检查所有打开的 Panel 均可关闭，并且暂停状态和输入阻塞能恢复。
