using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Dialogue.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Character.View
{
    public sealed class GameplayInputReader : MonoBehaviour
    {
        [SerializeField] private KeyCode dodgeKey = KeyCode.Space;
        [SerializeField] private KeyCode inventoryKey = KeyCode.Tab;
        [SerializeField] private KeyCode diaryKey = KeyCode.I;
        [SerializeField] private KeyCode mapKey = KeyCode.M;
        [SerializeField] private KeyCode reloadKey = KeyCode.R;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private KeyCode sceneTransitionKey = KeyCode.F;

        private void Update()
        {
            var root = GameRoot.Instance;
            if (root == null || root.Context == null)
            {
                return;
            }

            var events = root.Context.Events;


            // 当前版本的临时调试模式；正式版发布前移除。
            if (Input.GetKeyDown(KeyCode.F2))
            {
                events.Publish(new DebugModeToggledEvent());
            }
            if (root.GetSystem<DialogueSystem>()?.IsInputModeActive == true)
            {
                PublishDialogueAdvance(events);
                return;
            }

            PublishUiKeys(events);
            if (root.Context.UI != null && root.Context.UI.IsGameplayInputBlocked)
            {
                events.Publish(new MoveInputEvent(Vector2.zero));
                events.Publish(new RunInputEvent(false));
                return;
            }

            // 当前关卡是横向移动：只读取 A/D（Horizontal），不把 W/S 传入角色逻辑。
            events.Publish(new MoveInputEvent(new Vector2(Input.GetAxisRaw("Horizontal"), 0f)));
            events.Publish(new RunInputEvent(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)));
            // 右键的含义由 CharacterSystem 根据当前装备决定：枪械/投掷物瞄准，其余情况（包括空手）格挡。
            events.Publish(new SecondaryActionInputEvent(Input.GetMouseButton(1), GetPointerWorldPosition()));

            if (Input.GetMouseButtonDown(0))
            {
                events.Publish(new PrimaryActionPressedEvent());
            }

            if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(sceneTransitionKey))
            {
                events.Publish(new InteractPressedEvent());
            }

            if (Input.GetKeyDown(dodgeKey))
            {
                events.Publish(new DodgePressedEvent());
            }

            if (Input.GetKeyDown(reloadKey))
            {
                events.Publish(new ReloadPressedEvent());
            }

            PublishShortcutKeys(events);
        }

        private void PublishUiKeys(EventBus events)
        {
            if (Input.GetKeyDown(inventoryKey))
            {
                events.Publish(new OpenInventoryPressedEvent());
            }

            if (Input.GetKeyDown(diaryKey))
            {
                events.Publish(new OpenDiaryPressedEvent());
            }

            if (Input.GetKeyDown(mapKey))
            {
                events.Publish(new OpenMapPressedEvent());
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                events.Publish(new PausePressedEvent());
            }
        }

        private static void PublishShortcutKeys(EventBus events)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                events.Publish(new ShortcutEquipPressedEvent(0));
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                events.Publish(new ShortcutEquipPressedEvent(1));
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                events.Publish(new ShortcutEquipPressedEvent(2));
            }
        }

        private static void PublishDialogueAdvance(EventBus events)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                events.Publish(new DialogueAdvancePressedEvent());
            }
        }

        private static Vector2 GetPointerWorldPosition()
        {
            var camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                return Vector2.zero;
            }

            var screenPosition = Input.mousePosition;
            var worldPosition = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -camera.transform.position.z));
            return worldPosition;
        }
    }
}
