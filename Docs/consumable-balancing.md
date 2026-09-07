# 消耗品数值调整

在 Unity Project 中打开 `Assets/GameConfigs/Items`，选中对应道具，在 Inspector 的“消耗品效果数值”中修改并保存。

| 字段 | 含义 |
| --- | --- |
| Health Restore | 使用时恢复的生命值，最低 0；回满生命效果优先于该数值 |
| Effect Duration Seconds | 持续增益秒数，0 表示不添加持续增益，也不触发该增益的到期行为 |
| Stamina Cost Multiplier | 体力消耗倍率：1 正常，0 不消耗，0.5 减半 |
| Melee Damage Multiplier | 近战伤害倍率：1 正常，1.2 增加 20% |

1012–1019 的八个消耗品已填入原有数值。游戏从各道具 asset 读取上述字段，不从 Excel 读取。

Effect Id 保持不变，即使名称中含有旧数值也不需要修改；它用于选择回满体力、止血、解毒、鸦片酊到期惩罚等行为。回血量、倍率和时长以新增字段为准。

Effect Description 是独立文案，调整数值后需要手动同步。鸦片酊仍为使用时回满生命、效果到期恢复使用前生命并清空体力。
