using UnityEngine;

namespace MemorialArchive.Gameplay.Monster.Config
{
    public enum MonsterAttackMode
    {
        Melee,
        Ranged
    }

    [CreateAssetMenu(menuName = "Memorial Archive/Config/Monster")]
    public sealed class MonsterConfig : ScriptableObject
    {
        [SerializeField] private int monsterId;
        [SerializeField] private string monsterName;
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int attackDamage = 20;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float detectionRange = 5f;
        [SerializeField] private float attackRange = 1.2f;
        [SerializeField] private float attackInterval = 1f;
        [SerializeField] private float decisionInterval = 0.15f;
        [SerializeField] private float hurtDuration = 0.3f;
        [SerializeField] private float hurtCooldown = 1f;
        [SerializeField] private float deathPresentationSeconds = 2f;
        [SerializeField] private MonsterAttackMode attackMode;

        public int MonsterId => monsterId;
        public string MonsterName => monsterName;
        public int MaxHealth => maxHealth;
        public int AttackDamage => attackDamage;
        public float MoveSpeed => moveSpeed;
        public float DetectionRange => detectionRange;
        public float AttackRange => attackRange;
        public float AttackInterval => Mathf.Max(0.1f, attackInterval);
        public float DecisionInterval => Mathf.Clamp(decisionInterval, 0.1f, 0.2f);
        public float HurtDuration => Mathf.Max(0f, hurtDuration);
        public float HurtCooldown => Mathf.Max(hurtDuration, hurtCooldown);
        public float DeathPresentationSeconds => Mathf.Max(0f, deathPresentationSeconds);
        public MonsterAttackMode AttackMode => attackMode;
    }
}
