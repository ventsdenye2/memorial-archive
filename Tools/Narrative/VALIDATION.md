# 2026-09-12 验证记录

- Unity 2022.3.62f2c1 编译成功。
- NarrativeTests（8 项）与 SavePersistenceTests（1 项）共 9 项通过，0 失败。包含新增的旧存档缺失 narrative 模块时清除会话进度测试。
- NarrativeRuntimeTests 运行测试通过：首次办公室入场暂停和恢复；完成进度恢复后不重播；16 条记录事件打开笔记、使用正确字体、翻页读取；Cecil 两个实际按钮选项及淡出；露台返回三层指定出生点。
- 最终笔记排版再次使用 Unity TextGenerator 检查：16 条内容共 21 页，字符完整，正文高度全部在容器内。
- SceneLayoutValidator：15 场景通过，交互配置、Sprite、怪物预制体、边界内出生点和出口目的地无错误。
- Windows x64 Development 构建成功，输出 `Builds/Regression/Memorial Archive.exe`。
- 开发版独立进程存档写入和冷启动读取均退出码 0，日志均有 `SAVE_SMOKE_PASS`；仅使用 `Builds/Regression/SmokeSaves`，未触碰真实存档。该探针验证原有角色、背包/灯具、灯光模块；新增剧情模块的恢复由上述剧情测试覆盖。
- 最后一次开发包更新仅包含笔记正文高度调整；重新测量 21 页通过。
- OpeningStory 场景未修改，原 demo_win 未覆盖。

本机证据：`Logs/narrative-tests.txt`、`Logs/narrative-runtime.txt`、`Logs/narrative-build.txt`、`Logs/NarrativeQA/`、`Temp/SceneRebuildAudit/unity_scene_audit.json`、`Builds/Regression/narrative-smoke-write.log`、`Builds/Regression/narrative-smoke-read.log`。
