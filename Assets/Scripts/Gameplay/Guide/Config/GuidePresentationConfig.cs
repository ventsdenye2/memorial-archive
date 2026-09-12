using System;
using UnityEngine;
namespace MemorialArchive.Gameplay.Guide.Config
{
    [Serializable] public sealed class GuidePage
    {
        public string id;
        public Sprite artwork;
        public bool pause = true;
        public KeyCode dismissKey = KeyCode.None;
        [TextArea] public string message;
    }
    [CreateAssetMenu(menuName = "Memorial Archive/Config/Guide Presentation")]
    public sealed class GuidePresentationConfig : ScriptableObject
    {
        public GuidePage[] pages = Array.Empty<GuidePage>();
        public Sprite closeHint;
        public GameObject encounterPrefab;
        public GameObject overlayPrefab;
        public Sprite obstacleSprite;
        public Sprite diarySprite;
        public MemorialArchive.Gameplay.Interaction.Config.InteractionConfig diaryInteraction;
        public float frontHallLeft = -57.6f;
        public float equipmentGatePixels = 1920;
        public float specialLampPixels = 3842;
        public float diaryPixels = 4100;
        public float diaryInteractionWidth = 1.2f;
        public float obstacleInteractionWidth = 1.8f;
        public float warningIntervalPixels = 1920;
        public float darknessGraceSeconds = 3;
        public string frontHallRegion = "corridor_1f";
        public string treatmentScene = "Room_TreatmentA";
        public string encounterScene = "Floor_2F";
        public int keyItemId = 1024;
        public GuidePage Find(string id) => Array.Find(pages, p => p.id == id);
    }
}
