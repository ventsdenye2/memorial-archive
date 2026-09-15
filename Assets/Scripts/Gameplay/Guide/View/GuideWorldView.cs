using MemorialArchive.Framework.Core;
using MemorialArchive.Gameplay.Guide.Logic;
using MemorialArchive.Gameplay.Monster.View;
using MemorialArchive.Gameplay.Character.View;
using MemorialArchive.Gameplay.Lighting.View;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemorialArchive.Gameplay.Guide.View
{
    /// <summary>Projects saved teaching state into the loaded scene. All progress decisions belong to GuideFlowSystem.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class GuideWorldView : MonoBehaviour
    {
        private GuideFlowSystem flow;
        private GuideSystem guide;
        private LightSourceView[] sceneLights;
        private GameObject obstacle;
        private GameObject diary;
        private GameObject encounter;
        private int sceneHandle = -1;
        private void Start()
        {
            flow = GameRoot.Instance.GetSystem<GuideFlowSystem>();
            guide = GameRoot.Instance.GetSystem<GuideSystem>();
            // A tutorial may start in any scene; its canvas belongs to GameRoot, not to FrontHall.
            var overlay = Instantiate(flow.Config.overlayPrefab, transform, false).GetComponent<GuideOverlayPanel>();
            GameRoot.Instance.Context.UI.RegisterPersistentPanel(overlay);
        }
        private void Update()
        {
            if (flow?.Config == null || !flow.HasPosition) return;
            var scene = SceneManager.GetActiveScene();
            if (sceneHandle != scene.handle)
            {
                Clear(); sceneHandle = scene.handle;
                sceneLights = FindObjectsOfType<LightSourceView>();
            }
            if (flow.NeedsObstacle && obstacle == null)
            {
                obstacle = CreateSprite("GuideObstacle", flow.Config.obstacleSprite,
                    new Vector2(flow.Config.frontHallLeft + flow.Config.equipmentGatePixels / 100, flow.PlayerPosition.y));
            }
            if (!flow.NeedsObstacle && obstacle != null) { Destroy(obstacle); obstacle = null; }
            if (flow.NeedsDiary && diary == null)
            {
                diary = CreateSprite("GuideFirstDiary", flow.Config.diarySprite,
                    new Vector2(flow.Config.frontHallLeft + flow.Config.diaryPixels / 100, flow.PlayerPosition.y + .15f));
                var trigger = diary.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = new Vector2(flow.Config.diaryInteractionWidth, 2.4f);
                trigger.offset = new Vector2(0, -4.6f - diary.transform.position.y);
                diary.AddComponent<MemorialArchive.Gameplay.Interaction.View.InteractionPointView>().Configure(
                    flow.Config.diaryInteraction.InteractionId, flow.Config.diaryInteraction.InteractionType);
            }
            if (!flow.NeedsDiary && diary != null) { Destroy(diary); diary = null; }
            if (flow.NeedsEncounter && encounter == null)
            {
                var player = FindObjectOfType<PlayerMotor>();
                if (player == null || flow.Config.encounterPrefab == null) return;
                encounter = Instantiate(flow.Config.encounterPrefab, (Vector3)flow.EncounterPosition, Quaternion.identity);
                encounter.name = "GuideMeleeEncounter";
                encounter.GetComponentInChildren<MonsterTargetView>().SetRuntimeIdentity("guide_melee", 2001, flow.EncounterHealth);
            }
            if (flow.NeedsEncounter && encounter != null) flow.RecordEncounterPosition(encounter.transform.position);
            if (!flow.NeedsEncounter && encounter != null) { Destroy(encounter); encounter = null; }
        }
        private void LateUpdate()
        {
            if (guide == null || guide.HasCompleted("light") || flow == null || !flow.HasPosition || sceneLights == null) return;
            var camera = UnityEngine.Camera.main;
            if (camera == null || !camera.isActiveAndEnabled) return;
            foreach (var lamp in sceneLights)
            {
                if (lamp == null || lamp.gameObject.scene.handle != sceneHandle || !lamp.IsSpecial || !lamp.IsVisualVisible) continue;
                if ((camera.cullingMask & (1 << lamp.gameObject.layer)) == 0) continue;
                var bounds = lamp.VisualBounds;
                var screenPoint = camera.WorldToScreenPoint(bounds.center);
                var screenTop = camera.WorldToScreenPoint(bounds.center + Vector3.up * bounds.extents.y);
                var halfHeight = Mathf.Abs(screenTop.y - screenPoint.y);
                if (!IsInsideLightHighlight(screenPoint, new Vector2(Screen.width, Screen.height), halfHeight,
                    lamp.gameObject.scene.name == "FrontHall")) continue;
                guide.TryShowLightTutorial(true);
                break;
            }
        }

        public static bool IsInsideLightHighlight(Vector3 screenPoint, Vector2 screenSize, float screenHalfHeight = 0f, bool requireCentered = false)
        {
            if (screenPoint.z <= 0f || screenSize.x <= 0f || screenSize.y <= 0f) return false;
            // Tutorial page 3: (832, 402)-(1002, 589), measured from the top left of the 1920x1080 artwork.
            // Match preserveAspect letterboxing. Tall replacement lamps may extend below the authored window,
            // so require horizontal centering and vertical overlap instead of containment of their pivot.
            var scale = Mathf.Min(screenSize.x / 1920f, screenSize.y / 1080f);
            var offset = (screenSize - new Vector2(1920f, 1080f) * scale) * 0.5f;
            var point = ((Vector2)screenPoint - offset) / scale;
            var halfHeight = Mathf.Max(0f, screenHalfHeight) / scale;
            if (requireCentered)
                return point.x >= 832f && point.x < 1002f && point.y >= 491f && point.y < 678f;
            return point.x >= 832f && point.x < 1002f && point.y + halfHeight >= 491f && point.y - halfHeight < 678f;
        }

        private static GameObject CreateSprite(string name, Sprite sprite, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 45;
            if (sprite != null) go.transform.position += Vector3.up * sprite.bounds.extents.y;
            return go;
        }
        private void Clear()
        {
            if (obstacle != null) Destroy(obstacle);
            if (diary != null) Destroy(diary);
            if (encounter != null) Destroy(encounter);
            obstacle = diary = encounter = null;
        }
        private void OnDestroy() => Clear();
    }
}
