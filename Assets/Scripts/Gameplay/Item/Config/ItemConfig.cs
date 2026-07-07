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
        [SerializeField] private ItemCategory category = ItemCategory.Generic;
        [SerializeField] private InventoryFootprint backpackFootprint = InventoryFootprint.OneByOne;
        [SerializeField] private int maxStack = 1;
        [SerializeField] private bool canUse;
        [SerializeField] private bool canEquipToShortcut = true;
        [SerializeField] private bool canEquipToOffhand;
        [SerializeField] private OffhandType offhandType = OffhandType.None;
        [SerializeField] private bool canPlaceAmmo;
        [SerializeField] private int compatibleWeaponItemId;
        [SerializeField] private string effectId;

        public int ItemId => itemId;
        public string ItemName => itemName;
        public ItemCategory Category => category;
        public InventoryFootprint BackpackFootprint => backpackFootprint;
        public int MaxStack => Mathf.Max(1, maxStack);
        public bool CanUse => canUse;
        public bool CanEquipToShortcut => canEquipToShortcut;
        public bool CanEquipToOffhand => canEquipToOffhand;
        public OffhandType OffhandType => offhandType;
        public bool CanPlaceAmmo => canPlaceAmmo;
        public int CompatibleWeaponItemId => compatibleWeaponItemId;
        public string EffectId => effectId;
        public int BackpackWidth => backpackFootprint == InventoryFootprint.OneByTwoHorizontal ? 2 : 1;
        public int BackpackHeight => 1;
    }
}
