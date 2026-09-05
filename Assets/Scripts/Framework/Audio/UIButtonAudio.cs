using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MemorialArchive.Framework.Audio
{
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonAudio : MonoBehaviour, IPointerEnterHandler
    {
        private Button button;
        private void Awake() { button = GetComponent<Button>(); button.onClick.AddListener(Click); }
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Click); }
        private void Click() { if (button.IsInteractable()) AudioSystem.Play("sfx_ui_click"); }
        public void OnPointerEnter(PointerEventData e) { if (button.IsInteractable()) AudioSystem.Play("sfx_ui_hover"); }
    }
}
