using UnityEngine;

namespace MemorialArchive.Framework.Scene
{
    /// <summary>Compatibility for saves and routes authored before the hall/corridor merge.</summary>
    public static class FirstFloorSceneLayout
    {
        public const string SceneName = "FrontHall";
        public const string LegacySceneName = "Floor_1F";
        public const float CorridorOffsetX = 67.2f;
        public static string ResolveScene(string sceneId) => sceneId == LegacySceneName ? SceneName : sceneId;
        public static Vector3 ResolveSavedPosition(string sceneId, Vector3 position)
        {
            if (sceneId == LegacySceneName) position.x += CorridorOffsetX;
            return position;
        }
    }
}
