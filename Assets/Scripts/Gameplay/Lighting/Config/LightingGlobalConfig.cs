using UnityEngine;

namespace MemorialArchive.Gameplay.Lighting.Config
{
    /// <summary>
    /// 光照系统全局参数。所有数值集中在此配置，View 与 System 不得硬编码。
    /// 光源宽高单位为世界单位：项目 PPU=100，300px = 3.0。
    /// </summary>
    [CreateAssetMenu(menuName = "Memorial Archive/Config/Lighting Global")]
    public sealed class LightingGlobalConfig : ScriptableObject
    {
        [Header("黑暗层")]
        [SerializeField] private Color darknessColor = Color.black;
        [Range(0f, 1f)]
        [SerializeField] private float darknessAlpha = 0.95f;
        [SerializeField] private float darknessFadeSeconds = 0.4f;
        [SerializeField] private int maxSimultaneousLights = 32;
        [SerializeField] private Material darknessMaterial;
        [Range(1f, 6f)]
        [SerializeField] private float lightFalloffExponent = 2f;

        [Header("角色微光")]
        [Tooltip("角色微光的横向总宽度。与高度共同形成竖向椭圆光带；该光不计入交互门禁或黑暗失败判定。")]
        [SerializeField, Min(0.1f)] private float playerSafetyLightWidth = 1.5f;
        [Tooltip("角色微光的纵向总高度，覆盖角色头部到脚部。")]
        [SerializeField, Min(0.1f)] private float playerSafetyLightHeight = 3.4f;
        [Tooltip("角色微光中心亮度。1 为完全挖开暗幕，0 为不产生微光。")]
        [Range(0f, 1f)]
        [SerializeField] private float playerSafetyLightIntensity = 0.45f;
        [Tooltip("角色位置事件位于脚底，微光向上抬升后才能覆盖躯干。")]
        [SerializeField] private float playerSafetyLightYOffset = 0.9f;

        [Header("手提灯")]
        [SerializeField] private float lanternTotalFuelSeconds = 60f;
        [Tooltip("剩余燃料高于该值时为强光（默认 40 秒，即消耗前 20 秒）。")]
        [SerializeField] private float lanternStrongThresholdSeconds = 40f;
        [Tooltip("剩余燃料低于等于该值时为弱光（默认 10 秒，即最后 10 秒）。")]
        [SerializeField] private float lanternWeakThresholdSeconds = 10f;
        [Tooltip("手提灯的横向总宽度；光源保持竖向，不再是圆形点光。")]
        [SerializeField, Min(0.1f)] private float lanternStrongLightWidth = 2f;
        [SerializeField, Min(0.1f)] private float lanternNormalLightWidth = 1.8f;
        [SerializeField, Min(0.1f)] private float lanternWeakLightWidth = 1.6f;
        [Tooltip("手提灯的纵向总高度，用于完整照亮角色。")]
        [SerializeField, Min(0.1f)] private float lanternStrongLightHeight = 4.4f;
        [SerializeField, Min(0.1f)] private float lanternNormalLightHeight = 3.9f;
        [SerializeField, Min(0.1f)] private float lanternWeakLightHeight = 3.3f;
        [Tooltip("选中快捷栏中的手提灯时是否自动点亮。")]
        [SerializeField] private bool autoLightOnEquip = true;
        [Tooltip("手提灯光源的竖直抬升量（世界单位）。玩家事件位置在角色脚底原点，Spine 身体向上延伸，不抬升会让光圈压在脚下。")]
        [SerializeField] private float lanternLightYOffset = 0.9f;

        [Header("普通灯")]
        [SerializeField] private float tempLightSeconds = 10f;

        [Header("游戏失败")]
        [Tooltip("燃料耗尽且处于全黑时，通过标准伤害入口造成的致死伤害。")]
        [SerializeField] private float darknessFailureDamage = 9999f;
        [Tooltip("失败伤害的重试间隔，避免无敌帧/闪避帧吞掉致死伤害后不再触发。")]
        [SerializeField] private float failureRetrySeconds = 1f;

        public Color DarknessColor => darknessColor;
        public float DarknessAlpha => darknessAlpha;
        public float DarknessFadeSeconds => Mathf.Max(0.01f, darknessFadeSeconds);
        public int MaxSimultaneousLights => Mathf.Max(1, maxSimultaneousLights);
        public Material DarknessMaterial => darknessMaterial;
        public float LightFalloffExponent => lightFalloffExponent;
        public float PlayerSafetyLightWidth => Mathf.Max(0.1f, playerSafetyLightWidth);
        public float PlayerSafetyLightHeight => Mathf.Max(0.1f, playerSafetyLightHeight);
        public float PlayerSafetyLightIntensity => Mathf.Clamp01(playerSafetyLightIntensity);
        public float PlayerSafetyLightYOffset => playerSafetyLightYOffset;

        public float LanternTotalFuelSeconds => Mathf.Max(1f, lanternTotalFuelSeconds);
        public float LanternStrongThresholdSeconds => lanternStrongThresholdSeconds;
        public float LanternWeakThresholdSeconds => lanternWeakThresholdSeconds;
        public float LanternStrongLightWidth => Mathf.Max(0.1f, lanternStrongLightWidth);
        public float LanternNormalLightWidth => Mathf.Max(0.1f, lanternNormalLightWidth);
        public float LanternWeakLightWidth => Mathf.Max(0.1f, lanternWeakLightWidth);
        public float LanternStrongLightHeight => Mathf.Max(0.1f, lanternStrongLightHeight);
        public float LanternNormalLightHeight => Mathf.Max(0.1f, lanternNormalLightHeight);
        public float LanternWeakLightHeight => Mathf.Max(0.1f, lanternWeakLightHeight);
        public bool AutoLightOnEquip => autoLightOnEquip;
        public float LanternLightYOffset => lanternLightYOffset;

        public float TempLightSeconds => Mathf.Max(0.1f, tempLightSeconds);

        public float DarknessFailureDamage => Mathf.Max(1f, darknessFailureDamage);
        public float FailureRetrySeconds => Mathf.Max(0.1f, failureRetrySeconds);
    }
}
