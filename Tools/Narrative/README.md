# 剧情内容接入（2026-09-12）

源文案：剧情纸条0908.docx。按本次确认范围提取 16 条可阅读记录、10 段剧情（含阅读后的自言自语）。OpeningStory 未改；旧档案室、禁闭室暂不接入。

## 游戏行为

- 纸条、日记和档案互动后自动打开笔记，定位对应分类与条目；左右箭头翻页及切换条目。末页读完并关闭/切换后标记已读。
- 前厅、主管办公室、会客室、馆长办公室首次进入时显示场景叠加剧情。保留当前场景与 HUD，暂停探索，任意键推进；结束当帧仍拦截探索输入，防止推进点击同时攻击。
- 会客室 Cecil 按 F 交谈，两个按钮分别进入文档（1）（2）分支，随后合流。使用导入的 Spine 3.8 Cecil_idle，旁白结束时淡出并禁用交互。
- 馆长结论需要“馆长私人信件”和“特别赞助金流水账册”均已读；收集但未翻到末页不满足条件。
- narrative 存档模块保存收集、已读、已播及待播剧情。旧档迁移原 StorySystem 的收集 ID；旧档缺少新模块时清空当前会话的新模块状态，避免串档。
- 露台由独立 PNG 图层组成，三层最左侧入口进入，直接返回三层；不再通过馆长办公室。

## 文案对应

| 场景 | 记录 |
|---|---|
| 前厅 | 阿尔伯特日记其一、泛黄旧照片、无名纸条 |
| 主管办公室 | 行政与访客登记、阿尔伯特日记其二 |
| 档案室 A | 档案柜 A 合并记录、爱丽丝日记 |
| 档案室 B | 档案柜 B 合并记录 |
| 档案室 C | 档案柜 C 合并记录、威廉姆斯日记 |
| 治疗室 A | 看守巡查日志、带血纸条 |
| 治疗室 B | 诊疗记录 |
| 馆长办公室 | 私人信件、特别赞助金流水账册 |
| 露台 | 无名日记 |

## 字体与排版

使用用户提供的 Assets/Font/FZCHSJW.TTF（方正粗黑宋简体），同时保留 FZZJ-LZXTFSJW.TTF。与源字体 SHA-256 校验一致。

以 1920×1080 参考图实际字形大小映射设计点数：5 点对应 Canvas 20 像素，6 点对应 24 像素。设计字间距 12 按字体排版 tracking 的 12/1000 em 实现。

- 笔记标题：24 px、#4f3e37、tracking 12；正文：20 px、#59504c、tracking 0、28 px 基线行距。
- 自白页：20 px、#e3d7b2、tracking 12、40 px 基线行距。对话框和继续提示依参考图摆放；独立 Canvas 保证提示不被快捷栏遮住。
- 笔记依据实际 Font/TextGenerator 测量分页，保留完整原文；每页最多 340 字。

## 再生成

1. 修改/重新提取文案：`python -X utf8 Tools/Narrative/import_story.py <docx路径>`。段落索引有标题断言，换版文档应人工核对。
2. Unity 非运行状态执行 `Tools/Memorial Archive/Apply Narrative Content`。生成器可重复执行。
3. 如先运行旧 `Tools/SceneRebuild/apply_layout.py`，必须再执行上述剧情生成菜单，恢复文案交互配置及面板。旧布局脚本的露台路线也已同步修改。

## 验证入口

- NarrativeTests：内容链接、分页完整性、结论双文档条件、序列去重、旧存档隔离、交互配置和露台路线。
- NarrativeRuntimeTests：进入 PlayMode 实际验证首次入场暂停/恢复、恢复后不重播、16 条记录打开/阅读/字体高度、两条 Cecil 按钮分支与淡出、露台返回出生点。
- SavePersistenceTests：项目原有存档回归测试。
- `Logs/narrative-tests.txt`、`Logs/narrative-runtime.txt` 和 `Logs/NarrativeQA/` 是本机测试结果与截图。
- 可选开发队列：`Logs/narrative.request` 写入 apply / test / build，编辑器空闲时消费。build 输出到 `Builds/Regression/`，不覆盖 demo_win。

`unity_mcp.py` 仅连接项目配置中的 localhost:8080 Unity MCP，供本地可重复检查使用。
