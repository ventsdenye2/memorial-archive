using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using NUnit.Framework;
using UnityEditor;

namespace MemorialArchive.Tests.Editor
{
    public sealed class SceneAccessRuleTests
    {
        [TestCase("Floor_2F", "Room_Office", 1024)]
        [TestCase("Floor_3F", "Room_Director", 1028)]
        [TestCase("Floor_3F", "Floor_4F", 1027)]
        [TestCase("Room_Office", "Floor_2F", 0)]
        [TestCase("Floor_4F", "Floor_3F", 0)]
        [TestCase("Room_Terrace", "Room_Director", 0)]
        public void SceneAccess_KeysGateEntrancesWithoutLockingReturnRoutes(string source, string destination, int required)
        {
            var database = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>("Assets/GameConfigs/GameConfigDatabase.asset");
            var configs = new ConfigManager(database);
            configs.Initialize(new GameContext(new EventBus(), configs, null, null, null));
            Assert.That(configs.GetRequiredSceneKey(source, destination), Is.EqualTo(required));
            configs.Dispose();
        }
    }
}
