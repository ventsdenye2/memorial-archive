using System;
using System.Collections.Generic;
using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Guide.Config;
using MemorialArchive.Gameplay.Guide.Data;
using MemorialArchive.Gameplay.Item.Config;
using UnityEngine;

namespace MemorialArchive.Gameplay.Guide.Logic
{
    /// <summary>
    /// 配置驱动的通用引导系统。负责步骤推进、完成条件判定、并行步骤组、延时/自动隐藏、
    /// 进度存档与新游戏重置。发布 GuideStepStarted/Completed/Hidden 三个事件；
    /// Guide View 只根据事件显示，不自行推进步骤（分工约束）。
    ///
    /// 面板必须由 UIManager 以非模态覆盖层方式打开（见 UIManager.IsNonModalOverlay），
    /// 因此本系统不暂停游戏、不阻塞移动。完成条件一律判断真实结果，不依赖按钮点击。
    ///
    /// 显示/隐藏语义（对照需求 2.1/2.2）：
    /// - displayCondition != None 的步骤是「触发式」，不进顺序队列，条件首次满足时才显示
    ///   （如体力提示在玩家触发奔跑时弹出）；
    /// - hideTiming == Display 从显示起倒计时自动隐藏；== AfterComplete 等完成条件满足后
    ///   再倒计时 autoHideDelay 秒隐藏（如移动/背包提示在玩家移动后 3 秒消失）；
    /// - 显示期间完成的步骤若 autoHideDelay > 0，延迟同样秒数后必定补发 Hidden，条目不会常驻。
    /// </summary>
    public sealed class GuideSystem : IGameSystem, ITickableSystem, ISaveModule, INewGameResettable
    {
        private const string OperationGuideSequenceId = "operation";

        private readonly GuideSaveData saveData = new GuideSaveData();
        private readonly List<ActiveStep> activeSteps = new List<ActiveStep>();
        private readonly HashSet<string> conditionFlags = new HashSet<string>();

        private GameContext context;
        private GuideSequenceConfig activeSequence;
        private int sequenceCursor;
        private bool sequenceActive;

        public string ModuleKey => "guide";

        /// <summary>当前是否有引导序列在进行（供表现层初始化显隐状态）。</summary>
        public bool SequenceActive => sequenceActive;

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<SceneLoadedEvent>(HandleSceneLoaded);
            context.Events.Subscribe<MoveInputEvent>(HandleMoveInput);
            context.Events.Subscribe<RunInputEvent>(HandleRunInput);
            context.Events.Subscribe<PanelOpenedEvent>(HandlePanelOpened);
            context.Events.Subscribe<CharacterEquipmentChangedEvent>(HandleEquipmentChanged);
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<SceneLoadedEvent>(HandleSceneLoaded);
                context.Events.Unsubscribe<MoveInputEvent>(HandleMoveInput);
                context.Events.Unsubscribe<RunInputEvent>(HandleRunInput);
                context.Events.Unsubscribe<PanelOpenedEvent>(HandlePanelOpened);
                context.Events.Unsubscribe<CharacterEquipmentChangedEvent>(HandleEquipmentChanged);
            }

            context = null;
            StopSequence();
            conditionFlags.Clear();
            saveData.completedStepIds.Clear();
        }

        public void ResetForNewGame()
        {
            // 新游戏进入前厅时必须能完整跑通引导，因此清空已完成记录与运行时进度。
            saveData.completedStepIds.Clear();
            conditionFlags.Clear();
            StopSequence();
        }

        public void Tick(float deltaTime)
        {
            if (!sequenceActive)
            {
                return;
            }

            for (var i = activeSteps.Count - 1; i >= 0; i--)
            {
                var step = activeSteps[i];
                switch (step.State)
                {
                    case ActiveStepState.PendingDisplay:
                        if (!IsDisplayGateSatisfied(step.Config))
                        {
                            break;
                        }

                        step.DisplayTimer -= deltaTime;
                        if (step.DisplayTimer <= 0f)
                        {
                            step.State = ActiveStepState.Showing;
                            context.Events.Publish(new GuideStepStartedEvent(step.Config.StepId));
                            TryOpenOverlay();
                        }

                        break;

                    case ActiveStepState.Showing:
                        if (step.Config.HideTiming == GuideHideTiming.Display && step.AutoHideTimer > 0f)
                        {
                            step.AutoHideTimer -= deltaTime;
                            if (step.AutoHideTimer <= 0f)
                            {
                                step.State = ActiveStepState.Hidden;
                                context.Events.Publish(new GuideStepHiddenEvent(step.Config.StepId));
                            }
                        }

                        // 兜底完成检查：条件标志可能在步骤尚未显示时就已记录（如触发式的体力提示）。
                        if (step.State == ActiveStepState.Showing
                            && step.Config.CompleteCondition != GuideCompleteCondition.None
                            && step.Config.CompleteCondition != GuideCompleteCondition.Manual
                            && IsConditionSatisfied(step.Config.CompleteCondition))
                        {
                            CompleteStepInternal(step);
                        }

                        break;

                    case ActiveStepState.LingeringAfterComplete:
                        step.PostCompleteTimer -= deltaTime;
                        if (step.PostCompleteTimer <= 0f)
                        {
                            context?.Events.Publish(new GuideStepHiddenEvent(step.Config.StepId));
                            activeSteps.RemoveAt(i);
                        }

                        break;
                }
            }
        }

        public object CaptureSaveData() => saveData;

        public void RestoreSaveData(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var restored = JsonUtility.FromJson<GuideSaveData>(json);
            if (restored != null && restored.completedStepIds != null)
            {
                saveData.completedStepIds = restored.completedStepIds;
            }
        }

        /// <summary>手动标记一个 Manual 条件步骤完成（供外部逻辑调用）。</summary>
        public void CompleteStep(string stepId)
        {
            if (!string.IsNullOrEmpty(stepId))
            {
                CompleteMatchingSteps(config => config.CompleteCondition == GuideCompleteCondition.Manual
                                                && config.StepId == stepId);
            }
        }

        /// <summary>在当前激活序列中按 stepId 查步骤配置，供 View 展示。未激活时返回 null。</summary>
        public GuideStepConfig FindStepConfig(string stepId)
        {
            if (activeSequence == null || activeSequence.Steps == null || string.IsNullOrEmpty(stepId))
            {
                return null;
            }

            foreach (var step in activeSequence.Steps)
            {
                if (step != null && step.StepId == stepId)
                {
                    return step;
                }
            }

            return null;
        }

        private void HandleSceneLoaded(SceneLoadedEvent evt)
        {
            if (string.IsNullOrEmpty(evt.SceneId))
            {
                return;
            }

            // 找到以该场景为触发点的引导序列。
            var sequence = FindSequenceForScene(evt.SceneId);
            if (sequence != null)
            {
                StartSequence(sequence);
            }
        }

        private void HandleMoveInput(MoveInputEvent evt)
        {
            if (evt.Direction.sqrMagnitude > 0.0001f)
            {
                if (conditionFlags.Add(PlayerMovedFlag))
                {
                    EvaluateConditionDrivenSteps();
                }
            }
        }

        private void HandleRunInput(RunInputEvent evt)
        {
            if (evt.IsRunning && conditionFlags.Add(PlayerRunningFlag))
            {
                EvaluateConditionDrivenSteps();
            }
        }

        private void HandlePanelOpened(PanelOpenedEvent evt)
        {
            if (evt.PanelId == PanelId.Inventory && conditionFlags.Add(InventoryOpenedFlag))
            {
                EvaluateConditionDrivenSteps();
            }
        }

        private void HandleEquipmentChanged(CharacterEquipmentChangedEvent evt)
        {
            // 手提灯(1006)装备到副手(Lantern)即视为「道具已装备」条件满足。
            // 按装备结果判定而非绑定快捷栏/副手，规避物品配置归属的不确定性。
            if (evt.OffhandType == OffhandType.Lantern && conditionFlags.Add(ItemEquippedFlag))
            {
                EvaluateConditionDrivenSteps();
            }
        }

        private void StartSequence(GuideSequenceConfig sequence)
        {
            StopSequence();
            activeSequence = sequence;
            sequenceCursor = 0;
            sequenceActive = true;

            // 触发式步骤不进顺序队列，序列开始即挂起等待各自条件触发显示。
            var steps = sequence.Steps;
            if (steps != null)
            {
                foreach (var config in steps)
                {
                    if (config == null
                        || !IsTriggerDriven(config)
                        || saveData.completedStepIds.Contains(config.StepId))
                    {
                        continue;
                    }

                    activeSteps.Add(new ActiveStep
                    {
                        Config = config,
                        DisplayTimer = config.DisplayDelay,
                        AutoHideTimer = config.AutoHideDelay,
                        State = ActiveStepState.PendingDisplay
                    });
                }
            }

            // 序列激活状态事件：HUD 等表现层据此隐藏引导阶段不该出现的图标（需求 2.1 第 8 点）。
            context?.Events.Publish(new GuideSequenceActiveChangedEvent(true));
            Advance();
        }

        private void StopSequence()
        {
            var wasActive = sequenceActive;
            foreach (var step in activeSteps)
            {
                if (step.State == ActiveStepState.Showing || step.State == ActiveStepState.LingeringAfterComplete)
                {
                    context?.Events.Publish(new GuideStepHiddenEvent(step.Config.StepId));
                }
            }

            activeSteps.Clear();
            activeSequence = null;
            sequenceCursor = 0;
            sequenceActive = false;
            if (wasActive)
            {
                context?.Events.Publish(new GuideSequenceActiveChangedEvent(false));
            }
        }

        /// <summary>
        /// 推进到下一个独占步骤（或其所在并行组）。已完成(completedStepIds)与触发式步骤跳过。
        /// </summary>
        private void Advance()
        {
            if (activeSequence == null)
            {
                return;
            }

            var steps = activeSequence.Steps;
            if (steps == null || sequenceCursor >= steps.Length)
            {
                sequenceActive = false;
                return;
            }

            // 跳过已完成、空引用与触发式步骤（触发式已在 StartSequence 挂起）。
            while (sequenceCursor < steps.Length)
            {
                var config = steps[sequenceCursor];
                if (config == null || IsTriggerDriven(config))
                {
                    sequenceCursor++;
                    continue;
                }

                if (saveData.completedStepIds.Contains(config.StepId))
                {
                    sequenceCursor++;
                    continue;
                }

                break;
            }

            if (sequenceCursor >= steps.Length)
            {
                sequenceActive = false;
                return;
            }

            var head = steps[sequenceCursor];
            var groupId = head.ParallelGroupId;
            var startedThisAdvance = new List<GuideStepConfig>();

            if (string.IsNullOrEmpty(groupId))
            {
                startedThisAdvance.Add(head);
            }
            else
            {
                // 收集所有同 group 的步骤（通常连续，但容忍乱序），按 sortOrder 升序。
                for (var i = sequenceCursor; i < steps.Length; i++)
                {
                    var candidate = steps[i];
                    if (candidate == null || IsTriggerDriven(candidate) || candidate.ParallelGroupId != groupId)
                    {
                        continue;
                    }

                    if (saveData.completedStepIds.Contains(candidate.StepId))
                    {
                        continue;
                    }

                    startedThisAdvance.Add(candidate);
                }

                startedThisAdvance.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            }

            foreach (var config in startedThisAdvance)
            {
                activeSteps.Add(new ActiveStep
                {
                    Config = config,
                    DisplayTimer = config.DisplayDelay,
                    AutoHideTimer = config.AutoHideDelay,
                    State = ActiveStepState.PendingDisplay
                });
            }

            // 并行组启动后，游标越过本组所有成员，避免重复启动。
            if (!string.IsNullOrEmpty(groupId))
            {
                while (sequenceCursor < steps.Length)
                {
                    var config = steps[sequenceCursor];
                    if (config != null && !IsTriggerDriven(config) && config.ParallelGroupId == groupId)
                    {
                        sequenceCursor++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else
            {
                sequenceCursor++;
            }

            // 启动后立即检查一次条件——玩家可能已在进入前就满足（如已开过背包）。
            EvaluateConditionDrivenSteps();
        }

        /// <summary>对所有未完成步骤评估完成条件。触发式步骤必须已显示，否则不判完成。</summary>
        private void EvaluateConditionDrivenSteps()
        {
            for (var i = activeSteps.Count - 1; i >= 0; i--)
            {
                var step = activeSteps[i];
                if (step.State == ActiveStepState.Completed || step.State == ActiveStepState.LingeringAfterComplete)
                {
                    continue;
                }

                if (step.State == ActiveStepState.PendingDisplay && IsTriggerDriven(step.Config))
                {
                    continue;
                }

                if (IsConditionSatisfied(step.Config.CompleteCondition))
                {
                    CompleteStepInternal(step);
                }
            }
        }

        private bool IsConditionSatisfied(GuideCompleteCondition condition)
        {
            switch (condition)
            {
                case GuideCompleteCondition.PlayerMoved:
                    return conditionFlags.Contains(PlayerMovedFlag);
                case GuideCompleteCondition.PlayerRunning:
                    return conditionFlags.Contains(PlayerRunningFlag);
                case GuideCompleteCondition.InventoryOpened:
                    return conditionFlags.Contains(InventoryOpenedFlag);
                case GuideCompleteCondition.ItemEquipped:
                    return conditionFlags.Contains(ItemEquippedFlag);
                default:
                    return false;
            }
        }

        private bool IsDisplayGateSatisfied(GuideStepConfig config)
        {
            return config.DisplayCondition == GuideCompleteCondition.None
                   || config.DisplayCondition == GuideCompleteCondition.Manual
                   || IsConditionSatisfied(config.DisplayCondition);
        }

        private static bool IsTriggerDriven(GuideStepConfig config)
        {
            return config.DisplayCondition != GuideCompleteCondition.None
                   && config.DisplayCondition != GuideCompleteCondition.Manual;
        }

        private void CompleteStepInternal(ActiveStep step)
        {
            var stepId = step.Config.StepId;
            var wasShowing = step.State == ActiveStepState.Showing;
            step.State = ActiveStepState.Completed;
            saveData.completedStepIds.Add(stepId);
            context?.Events.Publish(new GuideStepCompletedEvent(stepId));

            if (wasShowing && step.Config.AutoHideDelay > 0f)
            {
                // 已显示的步骤完成后按配置再停留 autoHideDelay 秒（如「玩家移动后 3 秒消失」），
                // 到点补发 Hidden，保证 View 条目一定会收起。
                step.State = ActiveStepState.LingeringAfterComplete;
                step.PostCompleteTimer = step.Config.AutoHideDelay;
            }
            else
            {
                if (wasShowing)
                {
                    // 无停留时间的步骤，完成时立即补发 Hidden 收起条目。
                    context?.Events.Publish(new GuideStepHiddenEvent(stepId));
                }

                // 未显示过（玩家抢先完成）或已自动隐藏的步骤没有可见条目，直接移除。
                activeSteps.Remove(step);
            }

            // 必做步骤完成后推进序列；并行组要等同组全部必做步骤完成，避免后续提示提前弹出。
            if (sequenceActive
                && step.Config.Required
                && !GroupHasPendingRequired(step.Config.ParallelGroupId, step))
            {
                Advance();
            }
        }

        /// <summary>同组是否还有未完成的必做步骤（停留收尾中的不算，其动作已完成）。</summary>
        private bool GroupHasPendingRequired(string groupId, ActiveStep except)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                return false;
            }

            foreach (var other in activeSteps)
            {
                if (other == except
                    || !other.Config.Required
                    || other.Config.ParallelGroupId != groupId)
                {
                    continue;
                }

                if (other.State == ActiveStepState.PendingDisplay || other.State == ActiveStepState.Showing)
                {
                    return true;
                }
            }

            return false;
        }

        private void CompleteMatchingSteps(Func<GuideStepConfig, bool> predicate)
        {
            for (var i = activeSteps.Count - 1; i >= 0; i--)
            {
                var step = activeSteps[i];
                if (step.State != ActiveStepState.Completed && step.State != ActiveStepState.LingeringAfterComplete
                                                            && predicate(step.Config))
                {
                    CompleteStepInternal(step);
                }
            }
        }

        private GuideSequenceConfig FindSequenceForScene(string sceneId)
        {
            if (context?.Configs == null || string.IsNullOrEmpty(sceneId))
            {
                return null;
            }

            // ConfigManager 只按 sequenceId 索引；这里遍历候选序列找 triggerSceneId 匹配。
            // 通过约定优先查「操作引导」是否以该场景触发。
            foreach (var sequenceId in CandidateSequenceIds)
            {
                var sequence = context.Configs.GetGuideSequence(sequenceId);
                if (sequence != null && sequence.TriggerSceneId == sceneId)
                {
                    return sequence;
                }
            }

            return null;
        }

        private void TryOpenOverlay()
        {
            // GuideOverlay 为非模态覆盖层，Open 不会暂停游戏或阻塞输入。
            context?.UI?.Open(PanelId.GuideOverlay);
        }

        private static readonly string[] CandidateSequenceIds = { OperationGuideSequenceId };

        private const string PlayerMovedFlag = "PlayerMoved";
        private const string PlayerRunningFlag = "PlayerRunning";
        private const string InventoryOpenedFlag = "InventoryOpened";
        private const string ItemEquippedFlag = "ItemEquipped";

        private enum ActiveStepState
        {
            PendingDisplay,
            Showing,
            Hidden,
            Completed,
            LingeringAfterComplete
        }

        private sealed class ActiveStep
        {
            public GuideStepConfig Config;
            public float DisplayTimer;
            public float AutoHideTimer;
            public float PostCompleteTimer;
            public ActiveStepState State;
        }
    }
}
