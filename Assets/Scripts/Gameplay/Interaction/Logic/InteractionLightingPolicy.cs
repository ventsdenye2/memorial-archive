using MemorialArchive.Gameplay.Interaction.Data;

namespace MemorialArchive.Gameplay.Interaction.Logic
{
    /// <summary>
    /// 统一定义黑暗环境下的交互准入规则。
    /// 场景出口用于脱离危险，灯具用于恢复照明，两者不能被黑暗本身锁死。
    /// </summary>
    public static class InteractionLightingPolicy
    {
        public static bool CanAttemptInDarkness(InteractionType interactionType)
        {
            return interactionType == InteractionType.SceneExit ||
                   interactionType == InteractionType.LightSource;
        }
    }
}
