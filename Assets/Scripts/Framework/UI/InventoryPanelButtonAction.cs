using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    public enum InventoryPanelButtonActionType
    {
        Close = 0,
        Equip = 1,
        Discard = 2
    }

    [RequireComponent(typeof(Button))]
    public sealed class InventoryPanelButtonAction : MonoBehaviour
    {
        [SerializeField] private InventoryPanel panel;
        [SerializeField] private InventoryPanelButtonActionType actionType;

        public InventoryPanelButtonActionType ActionType => actionType;

        public void Configure(InventoryPanel owner, InventoryPanelButtonActionType action)
        {
            panel = owner;
            actionType = action;
        }

        private void Awake()
        {
            if (panel == null)
            {
                panel = GetComponentInParent<InventoryPanel>();
            }

            GetComponent<Button>().onClick.AddListener(InvokeAction);
        }

        private void OnDestroy()
        {
            var button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveListener(InvokeAction);
            }
        }

        private void InvokeAction()
        {
            switch (actionType)
            {
                case InventoryPanelButtonActionType.Close:
                    panel?.CloseFromButton();
                    break;
                case InventoryPanelButtonActionType.Equip:
                    panel?.EquipFromButton();
                    break;
                case InventoryPanelButtonActionType.Discard:
                    panel?.DiscardFromButton();
                    break;
            }
        }
    }
}
