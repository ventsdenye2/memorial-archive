using UnityEngine;

namespace MemorialArchive.Gameplay.Guide.Config
{
    /// <summary>
    /// 单个引导步骤的完成条件。GuideSystem 据此判定真实结果，而非依赖某个按钮被点击。
    /// </summary>
    public enum GuideCompleteCondition
    {
        /// <summary>无自动条件：仅由 autoHideDelay 或后续步骤推进时收尾。</summary>
        None,

        /// <summary>玩家产生过移动输入（MoveInputEvent 方向非零）。</summary>
        PlayerMoved,

        /// <summary>玩家进入过奔跑状态（RunInputEvent 为 true）。</summary>
        PlayerRunning,

        /// <summary>玩家打开过背包面板（PanelOpenedEvent 且 PanelId == Inventory）。</summary>
        InventoryOpened,

        /// <summary>指定道具被装备（CharacterEquipmentChangedEvent 且 OffhandType 对应，或 primaryItemId 命中）。
        /// 目标 itemId 由 <see cref="completeConditionArg"/> 提供。</summary>
        ItemEquipped,

        /// <summary>完全由外部逻辑调用 GuideSystem.CompleteStep(stepId) 收尾。</summary>
        Manual
    }

    /// <summary>
    /// 自动隐藏的计时时机，搭配 <see cref="autoHideDelay"/> 使用。
    /// </summary>
    public enum GuideHideTiming
    {
        /// <summary>从提示显示开始倒计时，到点隐藏（如奔跑提示显示 3 秒后消失）。</summary>
        Display,

        /// <summary>等完成条件满足后再倒计时隐藏（如需求「玩家移动后 3 秒消失」的移动/背包提示）。</summary>
        AfterComplete
    }

    /// <summary>
    /// 单个引导步骤的配置。所有数值必须来自配置，不得在 View 里硬编码。
    /// 同一 <see cref="parallelGroupId"/>（非空）的步骤会同时显示；空字符串表示独占顺序步骤。
    /// </summary>
    [CreateAssetMenu(menuName = "Memorial Archive/Config/Guide Step")]
    public sealed class GuideStepConfig : ScriptableObject
    {
        [SerializeField] private string stepId;
        [SerializeField] private string title;
        [TextArea] [SerializeField] private string description;
        [SerializeField] private Sprite sprite;
        [SerializeField] private string parallelGroupId = string.Empty;

        [Tooltip("进入该步骤后延时多少秒再发 GuideStepStartedEvent。0 = 立即。")]
        [SerializeField] private float displayDelay;

        [Tooltip("发完 Started 后多少秒自动隐藏。0 = 不自动隐藏，需完成或后续步骤触发。")]
        [SerializeField] private float autoHideDelay;

        [Tooltip("自动隐藏计时时机：Display=从显示开始倒计时；AfterComplete=完成条件满足后再倒计时（如玩家移动后 3 秒消失）。")]
        [SerializeField] private GuideHideTiming hideTiming = GuideHideTiming.Display;

        [Tooltip("触发式显示：条件首次满足时才显示（如体力提示在触发奔跑时弹出）。None=按序列顺序显示。")]
        [SerializeField] private GuideCompleteCondition displayCondition = GuideCompleteCondition.None;

        [Tooltip("同一序列内的排序权重，升序处理。")]
        [SerializeField] private int sortOrder;

        [Tooltip("必做步骤：完成后才推进下一独占步骤；已完成的不重复出现。")]
        [SerializeField] private bool required = true;

        [SerializeField] private GuideCompleteCondition completeCondition = GuideCompleteCondition.None;

        [Tooltip("当 completeCondition == ItemEquipped 时填目标 itemId（如手提灯 1006）。其余条件可留空。")]
        [SerializeField] private int completeConditionArg;

        public string StepId => stepId;
        public string Title => title;
        public string Description => description;
        public Sprite Sprite => sprite;
        public string ParallelGroupId => parallelGroupId ?? string.Empty;
        public float DisplayDelay => Mathf.Max(0f, displayDelay);
        public float AutoHideDelay => Mathf.Max(0f, autoHideDelay);
        public GuideHideTiming HideTiming => hideTiming;
        public GuideCompleteCondition DisplayCondition => displayCondition;
        public int SortOrder => sortOrder;
        public bool Required => required;
        public GuideCompleteCondition CompleteCondition => completeCondition;
        public int CompleteConditionArg => completeConditionArg;
    }
}
