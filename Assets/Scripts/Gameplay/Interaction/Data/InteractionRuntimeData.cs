using System;

namespace MemorialArchive.Gameplay.Interaction.Data
{
    [Serializable]
    public sealed class InteractionRuntimeData
    {
        public string interactionId;
        public InteractionType interactionType;
        public bool isEnabled = true;
        public bool isCompleted;
        public string linkedContainerId;
    }

    [Serializable]
    public sealed class InteractionSaveData
    {
        public System.Collections.Generic.List<InteractionRuntimeData> interactions = new System.Collections.Generic.List<InteractionRuntimeData>();
        public System.Collections.Generic.List<RoomStateData> rooms = new System.Collections.Generic.List<RoomStateData>();
    }
}
