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
                if (step.State == ActiveStepState.PendingDisplay)
                {
                    step.DisplayTimer -= deltaTime;
                    if (step.DisplayTimer <= 0f)
                    {
                        step.State = ActiveStepState.Showing;
                        context.Events.Publish(new GuideStepStartedEvent(step.Config.StepId));
                        TryOpenOverlay();
                    }
                }
                else if (step.State == ActiveStepState.Showing && step.AutoHideTimer > 0f)
                {
                    step.AutoHideTimer -= deltaTime;
                    if (step.AutoHideTimer <= 0f)
                    {
                        step.State = ActiveStepState.Hidden;
                        context.Events.Publish(new GuideStepHiddenEvent(step.Config.StepId));
                    }
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
            Advance();
        }

        private void StopSequence()
        {
            foreach (var step in activeSteps)
            {
                if (step.State == ActiveStepState.Showing)
                {
                    context?.Events.Publish(new GuideStepHiddenEvent(step.Config.StepId));
                }
            }

            activeSteps.Clear();
            activeSequence = null;
            sequenceCursor = 0;
            sequenceActive = false;
        }

        /// <summary>
        /// 推进到下一个独占步骤（或其所在并行组）。已完成(completedStepIds)的步骤跳过。
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

            // 跳过已完成的步骤。
            while (sequenceCursor < steps.Length && saveData.completedStepIds.Contains(steps[sequenceCursor].StepId))
            {
                sequenceCursor++;
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
                    if (candidate == null || candidate.ParallelGroupId != groupId)
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
                while (sequenceCursor < steps.Length
                       && steps[sequenceCursor] != null
                       && steps[sequenceCursor].ParallelGroupId == groupId)
                {
                    sequenceCursor++;
                }
            }
            else
            {
                sequenceCursor++;
            }

            // 启动后立即检查一次条件——玩家可能已在进入前就满足（如已开过背包）。
            EvaluateConditionDrivenSteps();
        }

        /// <summary>对所有 Showing/PendingDisplay 步骤评估完成条件。</summary>
        private void EvaluateConditionDrivenSteps()
        {
            for (var i = activeSteps.Count - 1; i >= 0; i--)
            {
                var step = activeSteps[i];
                if (step.State == ActiveStepState.Completed)
                {
                    continue;
                }

                if (IsConditionSatisfied(step.Config))
                {
                    CompleteStepInternal(step);
                }
            }
        }

        private bool IsConditionSatisfied(GuideStepConfig config)
        {
            switch (config.CompleteCondition)
            {
                case GuideCompleteCondition.None:
                    return false;
                case GuideCompleteCondition.PlayerMoved:
                    return conditionFlags.Contains(PlayerMovedFlag);
                case GuideCompleteCondition.PlayerRunning:
                    return conditionFlags.Contains(PlayerRunningFlag);
                case GuideCompleteCondition.InventoryOpened:
                    return conditionFlags.Contains(InventoryOpenedFlag);
                case GuideCompleteCondition.ItemEquipped:
                    return conditionFlags.Contains(ItemEquippedFlag);
                case GuideCompleteCondition.Manual:
                    return false;
                default:
                    return false;
            }
        }

        private void CompleteStepInternal(ActiveStep step)
        {
            var stepId = step.Config.StepId;
            step.State = ActiveStepState.Completed;
            saveData.completedStepIds.Add(stepId);
            context?.Events.Publish(new GuideStepCompletedEvent(stepId));

            if (step.Config.AutoHideDelay <= 0f)
            {
                // 无自动隐藏的步骤，完成时补发 Hidden，让 View 收起。
                context?.Events.Publish(new GuideStepHiddenEvent(stepId));
            }

            activeSteps.Remove(step);

            // 必做步骤完成后推进序列；非必做步骤不阻塞推进。
            if (sequenceActive && step.Config.Required)
            {
                Advance();
            }
        }

        private void CompleteMatchingSteps(Func<GuideStepConfig, bool> predicate)
        {
            for (var i = activeSteps.Count - 1; i >= 0; i--)
            {
                var step = activeSteps[i];
                if (step.State != ActiveStepState.Completed && predicate(step.Config))
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
            Completed
        }

        private sealed class ActiveStep
        {
            public GuideStepConfig Config;
            public float DisplayTimer;
            public float AutoHideTimer;
            public ActiveStepState State;
        }
    }
}
