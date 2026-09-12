using MemorialArchive.Framework.Scene;
using NUnit.Framework;
using UnityEngine;

namespace MemorialArchive.Tests.Editor
{
    public sealed class FirstFloorSceneLayoutTests
    {
        [Test]
        public void LegacyCorridorSaveMovesToMergedCoordinatesExactlyOnce()
        {
            var old=new Vector3(140,-5.2f,0);
            var migrated=FirstFloorSceneLayout.ResolveSavedPosition("Floor_1F",old);
            Assert.That(migrated.x,Is.EqualTo(207.2f).Within(.001f));
            Assert.That(migrated.y,Is.EqualTo(old.y));
            var scene=FirstFloorSceneLayout.ResolveScene("Floor_1F");
            Assert.That(scene,Is.EqualTo("FrontHall"));
            Assert.That(FirstFloorSceneLayout.ResolveSavedPosition(scene,migrated),Is.EqualTo(migrated));
        }
        [TestCase("FrontHall")]
        [TestCase("Floor_2F")]
        [TestCase("Room_Toilet")]
        public void ExistingOtherSceneSavesKeepTheirCoordinates(string scene)
        {
            var p=new Vector3(2,-5.2f,0);
            Assert.That(FirstFloorSceneLayout.ResolveScene(scene),Is.EqualTo(scene));
            Assert.That(FirstFloorSceneLayout.ResolveSavedPosition(scene,p),Is.EqualTo(p));
        }
    }
}
