# Spine 特效接入说明

## 接入结论

美术包使用 Spine 3.8 导出，和项目当前的 spine-unity 3.8 运行时匹配。完整的一次性特效已放入 `Assets/Resources/SpineEffects`，运行时统一通过 `SpineEffectPlayer` 加载、播放并自动销毁，不需要逐个场景或 Prefab 手工拖引用。

角色和怪物的整套动作资源则直接更新现有 JSON、Atlas、PNG，保留原 `.meta` 和 GUID，所以原 Prefab 的 `SkeletonDataAsset` 引用不会丢失。

## 资源与程序触发映射

| 美术目录 | 动画 | 程序中的用途 | 当前触发点 |
| --- | --- | --- | --- |
| 单手攻击 | `act（single）1/2/3` | 玩家单手三段攻击 | 更新现有 `SingleHanded_Attack` 数据，沿用角色攻击状态机 |
| 双手攻击1/2/3 | `act1/act2/act3` | 玩家双手三段攻击 | 更新现有 `Act1/2/3_BothHands` 数据，沿用角色攻击状态机 |
| 玩家枪械动作 | `gun-ami`、`gun-change bullet`、`gun-hold`、`gun -shot` | 持枪、瞄准、换弹、射击 | 更新现有 `Gun_Action` 数据，沿用角色枪械表现 |
| 受击特效（+钢夹板受击） | `hurt`、`gangjiaban hurt` | 玩家受伤；装备钢夹板时切换专用版本 | `DamageAppliedEvent` |
| 持盾格挡成功 | `animation` | 玩家持盾并成功格挡时的火花 | `DamageAppliedEvent.Result.WasBlocked` |
| 手雷爆炸 | `idle` | 手雷结算范围伤害时播放爆炸 | `ThrowableProjectileView.ExplodeGrenade` |
| 手枪弹道 | `animation` | 手枪 1007 开火时的枪口弹道 | `FirearmShotFrameEvent`，在释放后约 0.2 秒驱动；从 `gun_example2` 骨骼位置发射，方向为枪口指向释放时目标点 |
| Enemy01整套动作 | `attack1/2`、`idle`、`walk`、`hurt`、`die` | Enemy01 动作和攻击命中事件 | 更新现有 Enemy01 SkeletonData |
| Enemy02整套动作 | `attack`、`idle`、`walk`、`hurt`、`die` | Enemy02 动作；识别 `shoushudao shot` 命中事件 | 更新现有 Enemy02 SkeletonData |
| Enemy02手术刀弹道 | `animation` | Enemy02 远程攻击的飞刀表现 | `MonsterAIView.CommitAttackHit` |
| buff | `animation` | 恢复体力或获得持续属性修正 | `CharacterItemEffectRequestedEvent` |
| 治疗 | `animation` | 回复生命或回满生命 | `CharacterItemEffectRequestedEvent` |

## 运行时约定

- 一次性特效通过 `SpineEffectPlayer.TryPlayAt` 或 `TryPlayFollowing` 创建。
- 特效默认按 Spine 动画时长销毁；手枪弹道 `dandao` 总长约 `4.0s`，但只反向播放可见的 `0.22s` 窗口，手术刀弹道仍裁为 `0.32s`。
- 手雷 Spine 资源缺失或导入失败时，仍会回退到原有圆形占位特效，避免完全没有反馈。
- 手枪弹道目前只绑定物品 ID `1007`；资源名明确为“手枪弹道”，没有套用到其他枪械。
- `dandao` 资源的可视基准轴是本地 `+X`，由 `SpineEffectPlayer` 从可见窗口末端（默认 `0.22s`）向 `0` 反向播放；`FirearmAttackView.visualAngleOffset`（Player Prefab 默认 `0`°）仅用于美术资源校准，不再叠加角色父节点旋转。
- `gun -shot` 当前从 `1.7s` 起播；`firearmShotDelayAfterReleaseSeconds` 默认 `0.2s`，命中检测与弹道特效在释放后约 `0.2s` 的同一射击帧触发。弹道特效只做水平朝向（右 `0°`、左 `180°`，再叠加 `visualAngleOffset`），水平前移 `8` 个世界单位；hitscan 仍沿枪口到目标点的真实斜向。
- 受击特效只处理玩家，怪物受击继续使用怪物整套动作里的 `hurt`。

## 美术导出注意事项

- 单手三段攻击的事件名称分别是 `attack3`、`attack`、`attack2`，顺序不一致；当前程序把这些事件都当作通用命中点，所以能工作，但建议后续统一为 `attack_hit`。
- 双手 `act2`、`act3` 没有攻击事件，当前依赖程序原有的动画进度兜底命中。若要严格对齐刀光/碰撞，请在 Spine 中补 `attack_hit` 事件。
- Enemy02 手术刀当前是“事件时显示飞刀，同时立即结算伤害”，不是可躲避的实体投射物。若设计要求玩家能看见飞行过程并闪避，需要把伤害结算改到投射物碰撞时。
- 美术包没有燃烧瓶专用 Spine 特效，燃烧瓶仍使用项目原占位表现。

## 验证

- Unity 重新导入、编译完成，无项目脚本编译错误。
- `SpineEffectAssetTests` 对 7 个 SkeletonData、8 个动画名进行 Resources 加载验证，8/8 通过。
- 全量 EditMode 共 50 项，其中本次新增 8 项全部通过；另有角色装备动画和消耗品背包测试各 1 项失败，单独复跑仍失败，失败代码路径不在本次 Spine 接入改动内。
