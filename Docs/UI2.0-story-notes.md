# UI2.0 剧情页接入核对

- 用户本轮明确映射：自白页用于 OpeningStory；HUD 中角色自言自语没有明确宿主和发布者，不能擅自接入。
- OpeningStory.unity 中名为 OpeningStoryFlowController 的对象实际挂载 DialogueSceneController，使用 opening_dialogue，并通过 OpeningDialoguePanel / DialoguePresentationView 展示。自白页的 `话框.png` 与 `按任意键继续.png` 应落在 OpeningDialoguePanel。
- 上轮误改的备用 BlackScreenStoryPanel 已恢复原状，避免修改未明确使用的面板。
- 剧情页 CG01.png 本身含“AI参考”标记；不要把带标注的参考图视为最终新美术。剧情页的 CG/剧情框没有得到本轮明确宿主映射，暂不替换 OpeningStory；当前已有 CG 绑定与缺少明确替代关系的 CG02–05 暂保留。
- 对话页与教程页1/2/3 也暂未发现能与现有 Dialogue/Guide 配置一一对应的宿主。教程页是全屏蒙版、定位图和关闭提示，而 GuideOverlayPanel 是事件驱动的标题/描述条；在没有步骤/键位映射前不硬接。
- 字体包中兰梓斜体仿宋目录表明安装文件未提供：不能声称已精确应用该字体，应在完成记录注明回退。
