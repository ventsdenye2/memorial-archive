using MemorialArchive.Gameplay.Combat.Data;
using UnityEngine;

namespace MemorialArchive.Gameplay.Item.Config
{
    public enum ItemCategory
    {
        Generic,
        Consumable,
        Weapon,
        Ammo,
        Key,
        Note,
        PuzzlePart,
        ShotgunPart,
        Offhand
    }

    public enum InventoryFootprint
    {
        OneByOne,
        OneByTwoHorizontal
    }

    public enum OffhandType
    {
        None,
        Splint,
        Lantern,
        Shield
    }

    [CreateAssetMenu(menuName = "Memorial Archive/Config/Item")]
    public sealed class ItemConfig : ScriptableObject
    {
        [SerializeField] private int itemId;
        [SerializeField] private string itemName;
        [TextArea(2, 5)] [SerializeField] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private ItemCategory category = ItemCategory.Generic;
        [Tooltip("保留配置表的原始 Type：1 近战、2 远程、3 食品、4 医疗、5 非功能性。")]
        [SerializeField] private int sourceType;
        [SerializeField] private bool isMissionItem;
        [SerializeField] private bool isConsumable;
        [Tooltip("保留配置表的原始 Quick Access Bar：0 不可装备、1 快捷栏、2 副手栏。")]
        [SerializeField] private int quickAccessBarType;
        [Tooltip("保留配置表的原始 Placeholder；0 表示不占玩家背包格。")]
        [SerializeField] private int sourcePlaceholder = 1;
        [SerializeField] private InventoryFootprint backpackFootprint = InventoryFootprint.OneByOne;
        [SerializeField] private int maxStack = 1;
        [SerializeField] private bool canStack;
        [SerializeField] private bool canUse;
        [SerializeField] private bool canEquipToShortcut = true;
        [SerializeField] private bool canEquipToOffhand;
        [SerializeField] private OffhandType offhandType = OffhandType.None;
        [SerializeField] private bool canPlaceAmmo;
        [SerializeField] private int compatibleWeaponItemId;
        [SerializeField] private string effectId;
        [TextArea(2, 5)] [SerializeField] private string effectDescription;
        [TextArea(1, 3)] [SerializeField] private string synthesisRule;
        [SerializeField] private int synthesisTargetItemId;
        [SerializeField] private int synthesisRequiredQuantity;
        [Tooltip("保留配置表中的刷新权重或数量范围。")]
        [SerializeField] private string spawnRule;
        [Header("消耗品效果数值")]
        [Tooltip("使用时恢复的生命值；回满生命类效果仍按回满处理。")]
        [Min(0f)] [SerializeField] private float healthRestore;
        [Tooltip("增益持续时间，单位秒；0 表示不添加持续增益。")]
        [Min(0f)] [SerializeField] private float effectDurationSeconds;
        [Tooltip("体力消耗倍率：1 正常，0 不消耗，0.5 减半。")]
        [Min(0f)] [SerializeField] private float staminaCostMultiplier = 1f;
        [Tooltip("近战伤害倍率：1 正常，1.2 提升 20%。")]
        [Min(0f)] [SerializeField] private float meleeDamageMultiplier = 1f;

        [Header("Combat (only used when Category is Weapon)")]
        [SerializeField] private CombatAttackKind combatAttackKind = CombatAttackKind.Melee;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField] private int damage = 1;
        [SerializeField] private float attackRange = 1f;
        [SerializeField] private float attackActiveSeconds = 0.35f;
        [SerializeField] private float attackCooldownSeconds = 0.4f;
        [SerializeField] private float staminaCost = 1f;

        public float HealthRestore => Mathf.Max(0f, healthRestore);
        public float EffectDurationSeconds => Mathf.Max(0f, effectDurationSeconds);
        public float StaminaCostMultiplier => Mathf.Max(0f, staminaCostMultiplier);
        public float MeleeDamageMultiplier => Mathf.Max(0f, meleeDamageMultiplier);
        public int ItemId => itemId;
        public string ItemName => itemName;
        public string Description => description;
        public Sprite Icon => icon;
        public ItemCategory Category => category;
        public int SourceType => sourceType;
        public bool IsMissionItem => isMissionItem;
        public bool IsConsumable => isConsumable;
        public int QuickAccessBarType => quickAccessBarType;
        public int SourcePlaceholder => sourcePlaceholder;
        public InventoryFootprint BackpackFootprint => backpackFootprint;
        public int MaxStack => Mathf.Max(1, maxStack);
        public bool CanStack => canStack;
        public bool CanUse => canUse;
        public bool CanEquipToShortcut => canEquipToShortcut;
        public bool CanEquipToOffhand => canEquipToOffhand;
        public OffhandType OffhandType => offhandType;
        public bool CanPlaceAmmo => canPlaceAmmo;
        public int CompatibleWeaponItemId => compatibleWeaponItemId;
        public string EffectId => effectId;
        public string EffectDescription => effectDescription;
        public string SynthesisRule => synthesisRule;
        public int SynthesisTargetItemId => synthesisTargetItemId;
        public int SynthesisRequiredQuantity => synthesisRequiredQuantity;
        public string SpawnRule => spawnRule;
        public CombatAttackKind CombatAttackKind => combatAttackKind;
        public DamageType DamageType => damageType;
        public int Damage => Mathf.Max(0, damage);
        public float AttackRange => Mathf.Max(0f, attackRange);
        public float AttackActiveSeconds => Mathf.Max(0.01f, attackActiveSeconds);
        public float AttackCooldownSeconds => Mathf.Max(0f, attackCooldownSeconds);
        public float StaminaCost => Mathf.Max(0f, staminaCost);
        public int BackpackWidth => backpackFootprint == InventoryFootprint.OneByTwoHorizontal ? 2 : 1;
        public int BackpackHeight => 1;
    }
}
