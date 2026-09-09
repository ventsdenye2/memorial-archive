using System;
using UnityEngine;

namespace MemorialArchive.Framework.Scene
{
    [Serializable]
    public sealed class SceneAccessRule
    {
        [SerializeField] private string fromSceneId;
        [SerializeField] private string destinationSceneId;
        [SerializeField] private int requiredItemId;

        public string FromSceneId => fromSceneId;
        public string DestinationSceneId => destinationSceneId;
        public int RequiredItemId => requiredItemId;

        public bool Matches(string source, string destination) =>
            fromSceneId == source && destinationSceneId == destination;
    }
}
