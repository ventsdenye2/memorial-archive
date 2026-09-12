#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Character.Logic;
using MemorialArchive.Gameplay.Inventory.Logic;
using MemorialArchive.Gameplay.Lighting.Logic;
using UnityEngine;

namespace MemorialArchive.Framework.Save
{
    // Opt-in development-player regression. Uses an isolated directory, never real save slots.
    public sealed class SavePlayerSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (!Environment.GetCommandLineArgs().Contains("--save-smoke")) return;
            var obj = new GameObject("Save smoke probe");
            DontDestroyOnLoad(obj);
            obj.AddComponent<SavePlayerSmokeProbe>();
        }

        private IEnumerator Start()
        {
            var routine = Run();
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) break;
                    current = routine.Current;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Application.Quit(1);
                    yield break;
                }
                yield return current;
            }
            Debug.Log("SAVE_SMOKE_PASS");
            Application.Quit(0);
        }

        private IEnumerator Run()
        {
            yield return null;
            var root = GameRoot.Instance;
            Require(root != null && root.Context != null, "GameRoot unavailable");
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "SmokeSaves"));
            var events = new EventBus();
            var saves = new SaveManager(directory);
            saves.Initialize(new GameContext(events, root.Context.Configs, saves, null, null));
            var character = root.GetSystem<CharacterSystem>();
            var inventory = root.GetSystem<InventorySystem>();
            var lighting = root.GetSystem<LightingSystem>();
            saves.RegisterModule(character);
            saves.RegisterModule(inventory);
            saves.RegisterModule(lighting);
            if (Environment.GetCommandLineArgs().Contains("--save-smoke-write"))
            {
                root.Context.UI.CloseAll();
                root.Context.Scenes.LoadSavedScene("Floor_1F", new Vector3(2f, -5.2f));
                yield return null;
                yield return null;
                var lantern = inventory.PlayerInventory.playerItems.First(p => p.item.itemId == 1006);
                Require(inventory.TryMoveToShortcut(lantern.item.instanceId, 1), "Cannot move lantern");
                Require(inventory.TrySelectShortcut(1), "Cannot select lantern");
                for (var slot = 0; slot < SaveManager.Stage1SlotCount; slot++)
                {
                    events.Publish(new SaveRequestedEvent(slot));
                    Require(saves.HasSlot(slot), "Save failed");
                }
                events.Publish(new SaveRequestedEvent(0));
                Require(File.Exists(Path.Combine(directory, "save_slot_0.json.bak")), "Overwrite backup missing");
            }
            else
            {
                for (var slot = 0; slot < SaveManager.Stage1SlotCount; slot++)
                    Require(saves.HasSlot(slot), "Cold-start slot missing: " + slot);
                var saved = saves.GetSlot(0);
                Require(Application.CanStreamedLevelBeLoaded(saved.currentSceneId), "Saved scene missing in player");
                root.Context.UI.CloseAll();
                root.Context.Saves.RestoreSaveData(saved);
                var position = character.SavedScenePosition;
                root.Context.Scenes.LoadSavedScene(saved.currentSceneId, position);
                yield return null;
                yield return null;
                Require(inventory.GetSelectedShortcutPlacement()?.item?.itemId == 1006, "Lantern selection lost");
                Require(lighting.IsLanternEquipped, "Lantern not equipped after load");
                var player = GameObject.FindGameObjectWithTag("Player");
                Require(player != null && Vector2.Distance(player.transform.position, position) < 0.1f, "Saved position lost");
            }
            saves.Dispose();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
