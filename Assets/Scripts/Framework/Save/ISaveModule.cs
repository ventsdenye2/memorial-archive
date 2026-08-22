namespace MemorialArchive.Framework.Save
{
    public interface ISaveModule
    {
        string ModuleKey { get; }
        object CaptureSaveData();
        void RestoreSaveData(string json);
    }

    /// <summary>
    /// Optional contract for a save module that owns the player's world position.
    /// SaveManager only depends on this contract when it asks SceneFlowManager to
    /// restore a saved scene, keeping the scene system independent from gameplay
    /// module data.
    /// </summary>
    public interface ISaveScenePositionProvider
    {
        UnityEngine.Vector3 SavedScenePosition { get; }
    }
}
