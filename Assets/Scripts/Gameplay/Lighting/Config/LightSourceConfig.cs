using UnityEngine;

namespace MemorialArchive.Gameplay.Lighting.Config
{
    /// <summary>
    /// 单盏灯具配置。lightId 与场景内交互点的 interactionId 同名；
    /// regionId 为区域主键，特殊灯具交互后点亮同区域全部普通灯具。
    /// </summary>
    [CreateAssetMenu(menuName = "Memorial Archive/Config/Light Source")]
    public sealed class LightSourceConfig : ScriptableObject
    {
        [SerializeField] private string lightId;
        [SerializeField] private string regionId;
        [Tooltip("特殊灯具：交互后点亮区域内全部普通灯具并补满手提灯燃料；普通灯：交互后自身亮一段时间。")]
        [SerializeField] private bool isSpecial;
        [SerializeField] private float radius = 3f;
        [Tooltip("区域整体豁免黑暗（预留给露台等夜间常亮场景），本期不摆放。")]
        [SerializeField] private bool exemptFromDarkness;

        public string LightId => lightId;
        public string RegionId => regionId;
        public bool IsSpecial => isSpecial;
        public float Radius => Mathf.Max(0.1f, radius);
        public bool ExemptFromDarkness => exemptFromDarkness;
    }
}
