using UnityEngine;

namespace MemorialArchive.Gameplay.Combat.Data
{
    public static class CombatTargetIds
    {
        public const string Player = "player";
    }

    /// <summary>
    /// Identifies the presentation-side implementation that must consume an
    /// AttackStartedEvent.  The shared combat layer deliberately does not
    /// contain hitboxes, rays, or projectile movement.
    /// </summary>
    public enum CombatAttackKind
    {
        Melee,
        Firearm,
        Throwable
    }

    public enum DamageType
    {
        Physical,
        Bullet,
        Explosion,
        Fire
    }

    /// <summary>
    /// Immutable data for one attack attempt.  AttackInstanceId is generated
    /// by CombatSystem and must be returned in every later hit report.
    /// </summary>
    public sealed class AttackContext
    {
        public AttackContext(
            int attackInstanceId,
            string attackerId,
            string weaponInstanceId,
            int weaponItemId,
            CombatAttackKind attackKind,
            DamageType damageType,
            float baseDamage,
            float range,
            Vector2 origin,
            Vector2 direction,
            float activeSeconds,
            int comboStage = 0,
            bool hasTargetWorldPosition = false,
            Vector2 targetWorldPosition = default)
        {
            AttackInstanceId = attackInstanceId;
            AttackerId = attackerId;
            WeaponInstanceId = weaponInstanceId;
            WeaponItemId = weaponItemId;
            AttackKind = attackKind;
            DamageType = damageType;
            BaseDamage = Mathf.Max(0, baseDamage);
            Range = Mathf.Max(0f, range);
            Origin = origin;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            ActiveSeconds = Mathf.Max(0.01f, activeSeconds);
            ComboStage = Mathf.Clamp(comboStage, 0, 3);
            HasTargetWorldPosition = hasTargetWorldPosition;
            TargetWorldPosition = targetWorldPosition;
        }

        public int AttackInstanceId { get; }
        public string AttackerId { get; }
        public string WeaponInstanceId { get; }
        public int WeaponItemId { get; }
        public CombatAttackKind AttackKind { get; }
        public DamageType DamageType { get; }
        public float BaseDamage { get; }
        public float Range { get; }
        public Vector2 Origin { get; }
        public Vector2 Direction { get; }
        public float ActiveSeconds { get; }
        public bool HasTargetWorldPosition { get; }
        public Vector2 TargetWorldPosition { get; }
        /// <summary>近战为 1~3；枪械与投掷物为 0。</summary>
        public int ComboStage { get; }
    }

    /// <summary>
    /// Produced by a View after Unity collision/raycast/overlap detection.
    /// It intentionally contains no damage value: Views report facts, while
    /// CombatSystem remains the sole owner of damage rules.
    /// </summary>
    public readonly struct CombatHitReport
    {
        public CombatHitReport(int attackInstanceId, string targetId, Vector2 hitPoint, int hitSequence = 0)
        {
            AttackInstanceId = attackInstanceId;
            TargetId = targetId;
            HitPoint = hitPoint;
            HitSequence = Mathf.Max(0, hitSequence);
        }

        public int AttackInstanceId { get; }
        public string TargetId { get; }
        public Vector2 HitPoint { get; }
        /// <summary>通常为 0；持续伤害使用递增序号，让每个目标每个 tick 只受击一次。</summary>
        public int HitSequence { get; }
    }

    /// <summary>
    /// Input to the shared damage-resolution entry point.  Monster attacks may
    /// create this directly; player attack Views must normally use
    /// CombatHitReport so weapon configuration cannot be bypassed.
    /// </summary>
    public readonly struct DamageRequest
    {
        public DamageRequest(
            int attackInstanceId,
            string attackerId,
            string targetId,
            int weaponItemId,
            DamageType damageType,
            float rawDamage,
            Vector2 hitPoint)
        {
            AttackInstanceId = attackInstanceId;
            AttackerId = attackerId;
            TargetId = targetId;
            WeaponItemId = weaponItemId;
            DamageType = damageType;
            RawDamage = Mathf.Max(0f, rawDamage);
            HitPoint = hitPoint;
        }

        public int AttackInstanceId { get; }
        public string AttackerId { get; }
        public string TargetId { get; }
        public int WeaponItemId { get; }
        public DamageType DamageType { get; }
        public float RawDamage { get; }
        public Vector2 HitPoint { get; }
    }

    public readonly struct DamageResult
    {
        public DamageResult(float finalDamage, bool wasBlocked, bool killedTarget)
        {
            FinalDamage = Mathf.Max(0f, finalDamage);
            WasBlocked = wasBlocked;
            KilledTarget = killedTarget;
        }

        public float FinalDamage { get; }
        public bool WasBlocked { get; }
        // Target systems own their health, so this remains false until they
        // publish their own death event after applying DamageAppliedEvent.
        public bool KilledTarget { get; }
    }

    /// <summary>
    /// Optional extension point for target-side rules such as block, shield,
    /// armour, elemental resistance and temporary buffs.  Implementations are
    /// registered with CombatSystem; they must never mutate health directly.
    /// </summary>
    public interface IDamageModifier
    {
        bool AppliesTo(string targetId);
        DamageResult Modify(DamageRequest request, DamageResult currentResult);
    }

    /// <summary>
    /// CombatSystem 查询角色战斗快照的窄接口。角色仍拥有状态与体力，
    /// CombatSystem 不引用完整 CharacterSystem。
    /// </summary>
    public interface ICharacterCombatStateProvider
    {
        bool IsBlocking { get; }
        bool HasShieldEquipped { get; }
        void ConsumeSuccessfulBlockStamina(float amount);
    }
}
