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

        public static bool TryCreate(string effectId, out ItemEffectData effect)
        {
            effect = new ItemEffectData { EffectId = effectId };
            switch (effectId)
            {
                case "restore_full_stamina":
                    effect.RestoreFullStamina = true;
                    return true;
                case "no_stamina_cost_180":
                    effect.DurationSeconds = 180f;
                    effect.StaminaCostMultiplier = 0f;
                    return true;
                case "melee_damage_bonus_20_300":
                    effect.DurationSeconds = 300f;
                    effect.MeleeDamageMultiplier = 1.2f;
                    return true;
                case "restore_health_1_5":
                    effect.HealthRestore = 1.5f;
                    return true;
                case "restore_health_0_5_stamina_half_60":
                    effect.HealthRestore = 0.5f;
                    effect.DurationSeconds = 60f;
                    effect.StaminaCostMultiplier = 0.5f;
                    return true;
                case "restore_health_0_5_cure_bleeding":
                    effect.HealthRestore = 0.5f;
                    effect.CuresBleeding = true;
                    return true;
                case "restore_health_1_cure_poison":
                    effect.HealthRestore = 1f;
                    effect.CuresPoison = true;
                    return true;
                case "opium_tincture":
                    effect.RestoreFullHealth = true;
                    effect.DurationSeconds = 120f;
                    effect.StaminaCostMultiplier = 0f;
                    effect.MeleeDamageMultiplier = 1.2f;
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
