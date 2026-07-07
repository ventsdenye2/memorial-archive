namespace MemorialArchive.Framework.Save
{
    public interface ISaveModule
    {
        string ModuleKey { get; }
        object CaptureSaveData();
        void RestoreSaveData(string json);
    }
}
