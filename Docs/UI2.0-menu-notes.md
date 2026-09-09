# UI2.0 菜单迁移记录

## 已核对的素材

- `封面/背景.png`、`开始游戏弹窗/弹窗.png`、`游戏设置弹窗/设置弹窗背景.png` 均为 1920×1080 全屏合成底图，按 100 PPU 导入为 Sprite。
- 封面选项与暂停菜单选项为 438×132；封面红绳为 132×798，暂停菜单红绳为 43×642。
- 新游戏确认按钮普通态约 77×40，选中态约 176×111；取消按钮普通态约 79×39，选中态约 179×109。按钮保留 `SpriteSwapNativeSize`，因此悬停/选中时 RectTransform 会跟随 overrideSprite 的原生尺寸变化。
- 游戏设置按钮为 262×140，关闭按钮为 183×182；音量底框为 544×23，滑条填充为 273×23，手柄为 50×50。

## 定位与行为假设

`UI2MenuMigration.Apply()` 使用 1920×1080 画布中心坐标：新游戏确认/取消按钮约为 (-84,-98)/(192,-64)，暂停页四按钮约为 (0,185)/(0,10)/(0,-165)/(0,-340)，设置页全屏/窗口按钮约为 (-174,80)/(245,80)，分辨率箭头约为 (345,-90)。这些值来自对应 `定位页.png` 的 1:1 预览，若项目 Canvas Scaler 或实际参考分辨率改变，应在 Unity 中复核。

游戏设置页将“背景音效”接到 `AudioBus.Music`，“游戏音效”接到 `AudioBus.Sfx`；分辨率箭头循环 `Screen.resolutions`，全屏/窗口按钮调用 `Screen.fullScreenMode`。这是对现有 SettingsPanel 音频接口的最小补全。

## 字体与场景覆盖

当前字体包 zip 只含说明文本，兰梓目录没有可导入字体文件；迁移脚本保留 Unity LegacyRuntime 字体回退，不声称已应用新方正字体。`MainMenu.unity` 中 NewGameConfirmPanel 的两条旧 `m_SpriteState.m_HighlightedSprite` override 已改为 UI2.0 的“是的（选中）/取消（选中）” GUID。

## 验收入口

在 Unity 中执行 `Tools/Memorial Archive/Apply UI2.0 Menu`，保存四个 prefab 后打开 `MainMenu.unity`：确认封面、开始新游戏弹窗、暂停菜单、设置页的底图/按钮状态/点击事件；重点检查新游戏按钮 hover 时原生尺寸变化是否符合定位图。

## 本批实际交付与验收状态（2026-09-09）

本批工作树实际有内容改动的文件为：

- `Assets/Editor/UI2MenuMigration.cs`：主菜单/设置页迁移脚本；继续按钮、旧标题与副标题状态、分辨率默认文案，以及音量滑条的 `FillArea`/`HandleArea` 层级与布局。
- `Assets/Scripts/Framework/UI/SettingsPanel.cs`：分辨率按宽高去重，并在 `Screen.SetResolution` 异步切换期间立即写入请求值。
- `Assets/Prefabs/UI/MainMenuPanel.prefab`
- `Assets/Prefabs/UI/SettingsPanel.prefab`
- `Assets/Prefabs/UI/SavePanel.prefab`
- `Assets/Prefabs/UI/LoadPanel.prefab`

实际调用并成功返回的 Unity 菜单为：

- `Tools/Memorial Archive/Apply UI2.0 Menu`：保存 `MainMenuPanel`、`NewGameConfirmPanel`、`SystemPanel`、`SettingsPanel`。
- `Tools/Memorial Archive/Apply UI2.0 Save Layout`：保存 `SavePanel`、`LoadPanel`。

六个目标 prefab 均已由 Unity 序列化落盘：前四个菜单 prefab 的保存时间为 10:28，Save/Load 两个 prefab 的保存时间为 10:29。当前 Git 工作树中有内容差异的是 `MainMenuPanel`、`SettingsPanel`、`SavePanel`、`LoadPanel`；`NewGameConfirmPanel` 与 `SystemPanel` 保存后序列化内容与当前版本一致，因此没有 Git diff。

已验证的是静态序列化结果：主菜单 `ContinueButton` 位于定位坐标，旧 `Title` 已禁用、红绳 `Subtitle` 已启用；设置页分辨率文案为 `1920*1080 px`，两个音量滑条均有透明根节点和固定 `FillArea`/`HandleArea`；Save/Load 均包含 authored surface、`Slot_1..4`、`Status` 与取消按钮。已退出 prefab stage，当前活动场景恢复为 `Assets/Scenes/FrontHall.unity` 且未保存脏状态。

尚未验证的是 Play Mode 或真机行为：按钮点击/场景导航、hover/selected 时 `SpriteSwapNativeSize` 的实际视觉尺寸、真实分辨率切换、音量读写，以及 Save/Load 的真实存档写入与恢复流程。字体包仍只有说明文本，未声称已应用新的方正字体；定位坐标仍依赖 1920×1080 Canvas 参考分辨率，改变 Canvas Scaler 或参考分辨率后需要重新在 Unity 中复核。
