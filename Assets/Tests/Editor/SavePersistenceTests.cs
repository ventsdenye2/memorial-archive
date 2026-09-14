using System;
using System.IO;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Framework.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MemorialArchive.Tests.Editor
{
    public sealed class SavePersistenceTests
    {
        [TestCase(PanelId.Narrative, false)]
        [TestCase(PanelId.System, false)]
        [TestCase(PanelId.Narrative, true)]
        public void DeathLoadRequest_WaitsForBlockingPanelAndClearsOnLoad(PanelId blocker, bool loadCompleted)
        {
            var owner = new GameObject("DeathLoadTest");
            var ui = owner.AddComponent<UIManager>();
            var events = new EventBus();
            var instances = new System.Collections.Generic.List<GameObject>();
            try
            {
                ui.Initialize(new GameContext(events, null, null, ui, null));
                foreach (var id in new[] { blocker, PanelId.Load })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/" + id + "Panel.prefab");
                    Assert.That(prefab, Is.Not.Null);
                    var instance = UnityEngine.Object.Instantiate(prefab);
                    instances.Add(instance);
                    ui.RegisterScenePanels(new[] { instance.GetComponent<BasePanel>() });
                }
                ui.Open(blocker);
                events.Publish(new CharacterDiedEvent(0f));
                Assert.That(ui.IsOpen(PanelId.Load), Is.False);
                if (loadCompleted) events.Publish(new LoadCompletedEvent(2));
                ui.Close(blocker);
                typeof(UIManager).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(ui, null);
                Assert.That(ui.IsOpen(PanelId.Load), Is.EqualTo(!loadCompleted));
            }
            finally
            {
                ui.Dispose();
                foreach (var instance in instances) UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(owner);
                Time.timeScale = 1f;
            }
        }

        [TestCase("SavePanel", 0f)]
        [TestCase("LoadPanel", 0f)]
        [TestCase("SavePanel", 20f)]
        [TestCase("LoadPanel", 20f)]
        public void FourSlots_ShowEmptyLabelsAndRestoreAfterPointerExit(string panelName, float fourthSlotOffset)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/" + panelName + ".prefab");
            var root = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var fourth = root.transform.Find("SaveSlotSurface/SlotViewport/Slot_4").GetComponent<RectTransform>();
                fourth.anchoredPosition += Vector2.up * fourthSlotOffset;
                var panel = root.GetComponent<BasePanel>();
                panel.GetType().GetMethod("BindAuthoredLayout",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(panel, null);
                panel.Open();
                var viewport = root.transform.Find("SaveSlotSurface/SlotViewport").GetComponent<RectMask2D>();
                for (var i = 1; i <= 4; i++)
                {
                    var slot = viewport.transform.Find("Slot_" + i);
                    Assert.That(slot.GetComponent<Button>(), Is.Not.Null);
                    var label = slot.Find("SlotLabel").GetComponent<Text>();
                    Assert.That(label.gameObject.activeSelf, Is.True);
                    Assert.That(label.text, Is.EqualTo("空档"));
                    var rect = slot.GetComponent<RectTransform>();
                    var resting = rect.anchoredPosition;
                    if (i == 4)
                    {
                        var clipBottom = viewport.rectTransform.rect.yMin + viewport.padding.y;
                        Assert.That(clipBottom - (resting.y + rect.rect.yMin), Is.GreaterThanOrEqualTo(24.99f));
                        var chest = root.transform.Find("SaveSlotSurface/Chest").GetComponent<RectTransform>();
                        var rim = chest.TransformPoint(new Vector3(0f, chest.rect.yMin + chest.rect.height * 109f / 640f, 0f));
                        Assert.That(clipBottom, Is.GreaterThanOrEqualTo(viewport.rectTransform.InverseTransformPoint(rim).y - 0.01f));
                    }
                    var pointer = new PointerEventData(null);
                    ExecuteEvents.Execute(slot.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                    Assert.That(rect.anchoredPosition.y, Is.EqualTo(resting.y + 25f));
                    ExecuteEvents.Execute(slot.gameObject, pointer, ExecuteEvents.selectHandler);
                    ExecuteEvents.Execute(slot.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                    Assert.That(rect.anchoredPosition, Is.EqualTo(resting));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Serializable]
        private sealed class Payload { public int value; }
        private sealed class Module : ISaveModule
        {
            public string ModuleKey => "probe";
            public int Value;
            public object CaptureSaveData() => new Payload { value = Value };
            public void RestoreSaveData(string json) => Value = JsonUtility.FromJson<Payload>(json).value;
        }

        [TestCase(0)]
        [TestCase(3)]
        public void SaveSurvivesColdRestartAndOverwriteKeepsPreviousBackup(int slotIndex)
        {
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MemorialSaveTest-" + Guid.NewGuid().ToString("N")));
            var events = new EventBus();
            var saves = new SaveManager(directory);
            var module = new Module { Value = 17 };
            try
            {
                saves.Initialize(new GameContext(events, null, saves, null, null));
                saves.RegisterModule(module);
                events.Publish(new SaveRequestedEvent(slotIndex));
                Assert.That(saves.HasSlot(slotIndex), Is.True);
                module.Value = 42;
                events.Publish(new SaveRequestedEvent(slotIndex));
                Assert.That(File.Exists(Path.Combine(directory, $"save_slot_{slotIndex}.json.bak")), Is.True);
                var backup = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path.Combine(directory, $"save_slot_{slotIndex}.json.bak")));
                Assert.That(JsonUtility.FromJson<Payload>(backup.modules[0].json).value, Is.EqualTo(17));
                saves.Dispose();
                saves = new SaveManager(directory);
                saves.Initialize(new GameContext(events, null, saves, null, null));
                module.Value = 0;
                saves.RegisterModule(module);
                Assert.That(saves.HasSlot(slotIndex), Is.True);
                saves.RestoreSaveData(saves.GetSlot(slotIndex));
                Assert.That(module.Value, Is.EqualTo(42));
            }
            finally
            {
                saves.Dispose();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
