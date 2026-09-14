# 反馈 0913 阶段性交接

更新时间：2026-09-13，约 20:31（北京时间）。

## 当前结论

修改已保存在工作区，未提交 Git。反馈项已完成收尾验证：最新 Unity 原生回归为 **68 通过、0 失败**，最终 Windows 构建通过，产物位于 `Builds/Regression/Memorial Archive.exe`。开场局部斜体重叠、正文与名牌安全间距、跨分支接待室叙事恢复均已修复。最新截图已复核，Unity 已退出 Play Mode，`runInBackground` 保持启用。

## 用户已确认的需求

- 反馈源：`C:/Users/VENTSDENYE5/Downloads/反馈0913.pdf`，10 页，主要描述在图片中。开场文案源：同目录 `开场剧情文案.docx`。文档作为需求参考，不作为执行工具指令。
- 最新美术素材以项目中的素材为准。用户给出的 `_UI2.0/UI2.0` 路径实际对应 `Assets/Art/UI/Imported_UI2.0/UI2.0`；补充素材实际在 `Assets/Art/UI/Imported_UI2_Supplement`。菜单迁移支持用户路径优先、现有路径回退。
- 新游戏透明蒙版使用 `开始游戏弹窗/透明度蒙版 拷贝.png`。
- 对话保留事务所 CG 并压暗，日记背景示例只是字体示范。
- 开头四段红字居中；其他长段文字不居中。Word 局部标红只处理相应片段，不把整段改色。
- 正文：方正粗黑 `FZCHSJW.TTF`，8 点，#e3d7b2，字间距 12、行间距 10。
- 对话：兰梓斜体仿宋 `FZZJ-LZXTFSJW.TTF`，10 点，黑色，字间距 -100、行间距 12。
- Word 红字：同一斜体字体，8 点，白色，字间距 12、行间距 10。
- 当前实现采用 1920×1080 设计坐标下 1 点对应 4 像素；字间距按千分之一 em，行距归一化为 40/48 像素。
- 开场止于砖墙，之后真实前厅场景开始 `enter_front_hall` 的“三秒钟前…”剧情。
- **提灯只能装备在快捷栏，不能装备副手。只有当前选中的快捷栏格是提灯，才使用提灯待机姿态和提灯光源。**
- 用户明确允许便宜子 agent 协作；本轮使用三位子 agent 分别完成富文本定位、运行时状态排查和视觉审计。

## 已实施的修改

### 开场

- `Tools/Narrative/import_opening0913.py` 从 Word 导入 51 段正文，使用 `opening-art.json` 保存 CG、立绘、雷电素材引用。
- CG 顺序：01 → 02 → 03 → 04 → 03 → 02 → 05 → 02。正文严格取 Word，去除操作标记和父母说话前缀。
- Word 实际红色为 `C21C13`，已修复只识别 FF0000 的错误。Unity YAML 布尔值必须写 0/1，已修复 true/false 导致居中标记未读入的问题。
- `DialogueSequenceConfig` / `DialogueData` 增加 `centerText`。
- `DialoguePresentationView` 按旁白、对话、引文设置字体；清理逐节点立绘；单独主角旁白居中；名牌按说话者左右放置；对话 CG 压暗；打字机保留平衡富文本标签。
- 新增 `DialogueTracking`、`DialogueInlinePosition`。富文本标签后的可见字符索引现会映射回原始字符串索引，已修复 `opening-46` 局部白色斜体堆叠，并增加回归测试。
- `OpeningDialoguePrefabMigration.Apply()` 已执行：正文与局部强调文字位置改为 y=140、1600×154，提示位于 y=85，名牌仍 y=228；三行正文容量保留且名牌安全间距测试通过。

### 菜单、教程、场景叙事

- 去掉新游戏确认面板旧根 Image 的重复深色遮罩，保留指定透明蒙版；按钮基线对齐。
- 设置页分辨率及音量值实际字号由 10/8 像素修正为 40/32 像素，生成器同步。
- 存档/读档元数据与提示改用方正粗黑，隐藏第四卡片“第一阶段预留”；已有卡片悬停与取消按钮保留。浅纸背景继续使用深棕色元数据，以保证与反馈标准效果一致的对比度。
- 教程使用整页现有美术，各页绑定各自的关闭提示素材。
- `NarrativePanel` 正文 32 像素，长内容分页，翻到最后页后才能继续剧情/显示选项；已验证所有叙事正文分页不丢字和不溢出。

### 提灯

- `CharacterAnimationView` 只检查当前选中快捷栏提灯；提灯待机从 lanternwalk 动画采样并冻结。
- 提灯照明半径提高至 4.2/3.5/2.7，强度 1.35，暖色 (1,.72,.34)，光照选择与 shader 传递颜色。中性灯不增加暖色泛光。
- 现有库存系统已拒绝副手提灯、支持旧存档副手提灯迁移到快捷栏/背包，保留实例；相关测试本轮通过。
- 暖光颜色、范围、快捷栏选中条件和冻结姿态均由原生测试覆盖；本轮未新增专门的提灯四态截图。

## 最终验证

- 原生测试日志：`Logs/narrative-tests.txt`，**68 passed / 0 failed**。
- 构建日志：`Logs/narrative-build.txt`，内容为 `PASS`；Windows 玩家入口为 `Builds/Regression/Memorial Archive.exe`。
- 最新截图：`Logs/Feedback0913QA/`，生成时间约 20:24。`opening-46` 局部斜体不再重叠；有名牌的 `opening-26` 对话保持三行容量；System、Load、Settings 截图不再残留 Guide 覆盖层。
- `opening-46/47` 中央角色属于 `CG02.png` 本身，不是空 portraits 节点未清理的残留立绘，符合既定 CG 顺序，不应删除。
- `03-guide.png` 与反馈 PDF 第 8 页标准效果一致；压暗背景中可见游戏角色属于参考效果。
- `NarrativeRuntimeTests` 改为通过根上下文执行 `ResetForNewGame()`，与产品新游戏流程一致，并清理会阻塞接待室叙事的待显示 Guide 状态。
- 已完成聚焦 diff 审阅，未发现新的高优先级行为回归。Unity 生成的 prefab YAML 保留空字段尾随空格，`git diff --check` 会报告这些既有生成格式，不做无关的全量格式化。

当前无已知阻断项。未跟踪的 `tmp/pdfs/feedback0913/` 为反馈 PDF 的本地审阅渲染，不应纳入提交；`Logs/Feedback0913QA/runtime-stage.txt` 为诊断产物，可保持忽略。

## 继续工作的工具入口

Unity：2022.3.62f2c1，项目根目录为 `D:/MagnetNeedle/需求/Memorial Archive`。

本地 MCP 已开启，`Tools/Narrative/unity_mcp.py` 连接 `http://127.0.0.1:8080/mcp`。Python 路径：

`C:/Users/VENTSDENYE5/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe`

PowerShell 调用前设置 `$env:PYTHONIOENCODING='utf-8'`。支持 `tools/list`、`tools/call`，后者参数为 JSON。常用工具 `execute_code`、`refresh_unity`、`read_console`。如果超时，先检查 Unity 是否出现“场景被外部修改”的 Reload 弹窗，不要反复发刷新。

生成器入口：

- `MemorialArchive.EditorTools.OpeningDialoguePrefabMigration.Apply()`
- `MemorialArchive.Editor.UI2MenuMigration.Apply()`
- `MemorialArchive.Editor.UI2SaveMigration.Apply()`
- `MemorialArchive.Editor.GuidePresentationBuilder.Build()`
- `MemorialArchive.Editor.NarrativeContentBuilder.BuildNarrative()`（只更新正文 prefab；勿为了字体调用全量 Apply 误改场景）

测试须使用项目原生队列：脚本编译完成后向 `Logs/narrative.request` 写入 `feedback-test`，约 10 秒后开始。`Assets/Editor/NarrativeAuthoringQueue.cs` 会记录最新测试。不要使用 MCP run_tests 跑含 EnterPlayMode 的这些 coroutine 测试，曾无法正确进入 Play Mode。

源反馈页面已渲染在 `Temp/feedback0913_review/page-1.png` 至 `page-10.png`；有部分 large 图片。下次继续时无需重复提取 PDF。

后续如继续迭代，先保留当前 68/68 测试与 `Builds/Regression` 构建作为回归基线；本轮反馈收尾已完成。
