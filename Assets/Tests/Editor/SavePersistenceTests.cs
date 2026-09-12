using System;
using System.IO;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using NUnit.Framework;
using UnityEngine;

namespace MemorialArchive.Tests.Editor
{
    public sealed class SavePersistenceTests
    {
        [Serializable]
        private sealed class Payload { public int value; }
        private sealed class Module : ISaveModule
        {
            public string ModuleKey => "probe";
            public int Value;
            public object CaptureSaveData() => new Payload { value = Value };
            public void RestoreSaveData(string json) => Value = JsonUtility.FromJson<Payload>(json).value;
        }

        [Test]
        public void SaveSurvivesColdRestartAndOverwriteKeepsPreviousBackup()
        {
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MemorialSaveTest-" + Guid.NewGuid().ToString("N")));
            var events = new EventBus();
            var saves = new SaveManager(directory);
            var module = new Module { Value = 17 };
            try
            {
                saves.Initialize(new GameContext(events, null, saves, null, null));
                saves.RegisterModule(module);
                events.Publish(new SaveRequestedEvent(0));
                Assert.That(saves.HasSlot(0), Is.True);
                module.Value = 42;
                events.Publish(new SaveRequestedEvent(0));
                Assert.That(File.Exists(Path.Combine(directory, "save_slot_0.json.bak")), Is.True);
                var backup = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path.Combine(directory, "save_slot_0.json.bak")));
                Assert.That(JsonUtility.FromJson<Payload>(backup.modules[0].json).value, Is.EqualTo(17));
                saves.Dispose();
                saves = new SaveManager(directory);
                saves.Initialize(new GameContext(events, null, saves, null, null));
                module.Value = 0;
                saves.RegisterModule(module);
                Assert.That(saves.HasSlot(0), Is.True);
                saves.RestoreSaveData(saves.GetSlot(0));
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
