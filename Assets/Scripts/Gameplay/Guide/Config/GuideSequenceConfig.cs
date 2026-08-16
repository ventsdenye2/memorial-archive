using UnityEngine;

namespace MemorialArchive.Gameplay.Guide.Config
{
    /// <summary>
    /// 一个引导序列（如「操作引导」），由若干 <see cref="GuideStepConfig"/> 组成。
    /// steps 在运行时按 sortOrder 升序处理；同 parallelGroupId 的步骤并行显示。
    /// </summary>
    [CreateAssetMenu(menuName = "Memorial Archive/Config/Guide Sequence")]
    public sealed class GuideSequenceConfig : ScriptableObject
    {
        [SerializeField] private string sequenceId;
        [Tooltip("触发该序列的场景名（匹配 SceneLoadedEvent.SceneId）。空表示由外部手动启动。")]
        [SerializeField] private string triggerSceneId;
        [SerializeField] private GuideStepConfig[] steps;

        public string SequenceId => sequenceId;
        public string TriggerSceneId => triggerSceneId ?? string.Empty;
        public GuideStepConfig[] Steps => steps;
    }
}
