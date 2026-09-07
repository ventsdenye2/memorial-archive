using MemorialArchive.Gameplay.Item.Config;

namespace MemorialArchive.Gameplay.Item.Data
{
    /// <summary>Runtime description of a character-facing consumable effect.</summary>
    public sealed class ItemEffectData
    {
        public string EffectId { get; private set; }
        public float HealthRestore { get; private set; }
        public bool RestoreFullHealth { get; private set; }
        public bool RestoreFullStamina { get; private set; }
        public bool CuresBleeding { get; private set; }
        public bool CuresPoison { get; private set; }
        public float DurationSeconds { get; private set; }
        public float StaminaCostMultiplier { get; private set; } = 1f;
        public float MeleeDamageMultiplier { get; private set; } = 1f;
        public bool RestoreHealthAtExpiry { get; private set; }
        public bool ExhaustAtExpiry { get; private set; }

        public bool HasTimedModifier => DurationSeconds > 0f;

        public static bool TryCreate(ItemConfig config, out ItemEffectData effect)
        {
            effect = null;
            if (config == null)
            {
                return false;
            }

            effect = new ItemEffectData
            {
                EffectId = config.EffectId,
                HealthRestore = config.HealthRestore,
                DurationSeconds = config.EffectDurationSeconds,
                StaminaCostMultiplier = config.StaminaCostMultiplier,
                MeleeDamageMultiplier = config.MeleeDamageMultiplier
            };
            switch (config.EffectId)
            {
                case "restore_full_stamina":
                    effect.RestoreFullStamina = true;
                    return true;
                case "no_stamina_cost_180":
                    return true;
                case "melee_damage_bonus_20_300":
                    return true;
                case "restore_health_1_5":
                    return true;
                case "restore_health_0_5_stamina_half_60":
                    return true;
                case "restore_health_0_5_cure_bleeding":
                    effect.CuresBleeding = true;
                    return true;
                case "restore_health_1_cure_poison":
                    effect.CuresPoison = true;
                    return true;
                case "opium_tincture":
                    effect.RestoreFullHealth = true;
                    effect.RestoreHealthAtExpiry = true;
                    effect.ExhaustAtExpiry = true;
                    return true;
                default:
                    effect = null;
                    return false;
            }
        }
    }
}
