using UnityEngine;

namespace MemorialArchive.Gameplay.Monster.Config
{
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

        public int MonsterId => monsterId;
        public string MonsterName => monsterName;
        public int MaxHealth => maxHealth;
        public int AttackDamage => attackDamage;
        public float MoveSpeed => moveSpeed;
        public float DetectionRange => detectionRange;
        public float AttackRange => attackRange;
    }
}
