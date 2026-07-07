using UnityEngine;

namespace MemorialArchive.Gameplay.Character.Config
{
    [CreateAssetMenu(menuName = "Memorial Archive/Config/Character Attribute")]
    public sealed class CharacterAttributeConfig : ScriptableObject
    {
        [SerializeField] private int attributeId;
        [SerializeField] private string displayName;
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int maxStamina = 100;
        [SerializeField] private float walkSpeed = 2.4f;
        [SerializeField] private float runSpeed = 4f;
        [SerializeField] private float dodgeCooldownSeconds = 0.8f;

        public int AttributeId => attributeId;
        public string DisplayName => displayName;
        public int MaxHealth => maxHealth;
        public int MaxStamina => maxStamina;
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float DodgeCooldownSeconds => dodgeCooldownSeconds;
    }
}
