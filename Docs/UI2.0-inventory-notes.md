# UI2.0 背包页接入记录

## 已落地

- `InventoryPanel` 使用 `Assets/Art/UI/Imported_UI2.0/UI2.0/背包/物品.png`（678×551）作为左侧场景物品面板，中心按定位图 `(523,510)` 放置。
- `背包.png`（1068×960）作为右侧背包面板，中心按定位图 `(1345,497)` 放置。
- 左侧 2×2 使用 `物品格.png` / `物品格（选中）.png`，起点 `(491,457)`；右侧 3×3 使用 `背包格子.png` / `背包格（选中）.png`，起点 `(1165,164)`。格子按原尺寸无间距铺开。
- 装备、取消、退出和底部四格快捷栏改用 UI2.0 素材；`InventoryPanel` 的拖拽、点击、装备、丢弃和场景容器显隐绑定保留。
- 可在 Unity 菜单 `Tools/Memorial Archive/Apply UI2.0 Inventory Layout` 执行 `UI2InventoryMigration.Apply()`，重建三个 prefab 的静态布局。

## 字体与待核对项

- UI2.0 素材包里的 `字体标准.png` 只有标注图，配套 zip 只有 txt；工程也没有方正粗黑字体文件。当前保留 Unity `LegacyRuntime.ttf` 可读回退，未声称接入方正字体。
- 新 `背包.png` 已包含底部说明纸张，因此迁移会清空旧 `道具介绍框` 的叠加图，只保留可更新的说明文字。
- 新定位图中的底部四格与 `InventoryPanel` 的快捷栏目标位置重合；`ShortcutBarPanel.prefab` 同步做成同一组四格。场景若同时启用 HUD 自带快捷栏，请只保留一个可见实例。
- 若外部工具给背景 Image 添加 `SpriteSwapNativeSize`，请在运行时检查其 LateUpdate 是否覆盖定位尺寸；本迁移不在组合背景上添加该组件。
