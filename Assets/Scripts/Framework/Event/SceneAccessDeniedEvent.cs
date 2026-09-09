namespace MemorialArchive.Framework.Event
{
    public readonly struct SceneAccessDeniedEvent
    {
        public SceneAccessDeniedEvent(string sceneId, string message)
        {
            SceneId = sceneId;
            Message = message;
        }

        public string SceneId { get; }
        public string Message { get; }
    }
}
