using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using UnityEngine;

namespace MemorialArchive.Framework.UI
{
    public sealed class HUDButtonActions : MonoBehaviour
    {
        public void OpenSystem() => Publish(new PausePressedEvent());
        public void OpenInventory() => Publish(new OpenInventoryPressedEvent());
        public void OpenDiary() => Publish(new OpenDiaryPressedEvent());
        public void OpenMap() => Publish(new OpenMapPressedEvent());

        private static void Publish<T>(T evt)
        {
            GameRoot.Instance?.Context?.Events.Publish(evt);
        }
    }
}
