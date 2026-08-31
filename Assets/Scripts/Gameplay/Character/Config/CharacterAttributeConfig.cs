using UnityEngine;

namespace MemorialArchive.Gameplay.Character.Config
{
    [CreateAssetMenu(menuName = "Memorial Archive/Config/Character Attribute")]
    public sealed class CharacterAttributeConfig : ScriptableObject
    {
        [SerializeField] private int attributeId;
        [SerializeField] private string displayName;
        [SerializeField] private int maxHealth = 3;
        [SerializeField] private int maxStamina = 30;
        [SerializeField] private float walkSpeed = 100f;
        [SerializeField] private float runSpeed = 300f;
        [SerializeField] private float runStaminaCostPerSecond = 2f;
        [SerializeField] private float staminaRecoveryPerSecond = 1f;
        [SerializeField] private float dodgeStaminaCost = 6f;
        [SerializeField] private float dodgeDistance = 240f;
        [SerializeField] private float dodgeDurationSeconds = 0.18f;
        [SerializeField] private float dodgeCooldownSeconds = 0.8f;

        public int AttributeId => attributeId;
        public string DisplayName => displayName;
        public int MaxHealth => maxHealth;
        public int MaxStamina => maxStamina;
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float RunStaminaCostPerSecond => runStaminaCostPerSecond;
        public float StaminaRecoveryPerSecond => staminaRecoveryPerSecond;
        public float DodgeStaminaCost => dodgeStaminaCost;
        public float DodgeDistance => dodgeDistance;
        public float DodgeDurationSeconds => dodgeDurationSeconds;
        public float DodgeCooldownSeconds => dodgeCooldownSeconds;
    }
}
