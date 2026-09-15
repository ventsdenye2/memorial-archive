# 二楼和休息室剧情核查

## 本次范围

按本次确认，旧档案室、禁闭室暂不接入；馆长办公室保持原文案和原触发条件。

## 二楼修复

原配置将“拾级而上来到二楼……”和“一扇厚重的橡木门半掩着……”放在休息室入场序列中，Floor_2F 没有入场剧情。

现在两句由 enter_second_floor 在首次进入 Floor_2F 时自动播放，暂停探索，结束后恢复。enter_reception 从“我推开二楼休息室的门……”开始，保留三句室内描述。已播状态随存档保留，往返不重播。导入脚本同步修改。

## 休息室核查

- 当前工程通过实际场景转场验证：首次进入 Room_Reception 会自动显示室内描述和继续提示。
- 完成入场剧情、选中手提灯并靠近 Cecil，交互提示可见；通过 InteractPressedEvent（实际 F 键交互事件）能进入 cecil_conversation。
- 两条对话分支均能执行，结束后 Cecil 消散并禁用交互。
- 当前交互提示需要选中手提灯；NPC 对话还受照明准入规则约束。已播放的入场剧情和 NPC 对话不会在同一进度中重复播放。
- 测试最初因重置新手引导后直接跳入休息室，灯具教程弹出并暂停，从而阻塞后续 NPC 对话。按正常上楼前已完成灯具教程的进度修正测试后通过；这不能直接证明策划遇到的是同一个原因。

## 0914 包核查及限制

本机 Player.log 指向 D:/MagnetNeedle/0914/Memorial Archive.exe。对该包 resources.assets 中实际 JSON 的检查确认：包含 enter_reception（Room_Reception、onFirstEnter=true、5 句）和 cecil_conversation（10 句），没有 enter_second_floor。程序集中的场景事件发送、首次入场排队和剧情播放逻辑也存在。

因此确认二楼缺少独立触发；未发现该包缺少休息室文案。尚未在策划的具体操作和存档状态下复现“休息室提示与剧情都没有”，不能断言是旧包、存档或未持灯所致。此次没有重新打包或覆盖 0914 可执行文件。

## 验证

Unity NarrativeTests 与 NarrativeRuntimeTests：11 项通过，0 项失败。包括二楼自动入场暂停/恢复、存档后不重播、休息室首次入场、实际 NPC 交互提示与触发、两个对话分支，以及馆长仍需读完两份文档的回归。

本机结果：Logs/narrative-tests.txt。截图：Logs/NarrativeQA/second-floor-entry.png、reception-entry.png、cecil-choice-0.png、cecil-choice-1.png。
