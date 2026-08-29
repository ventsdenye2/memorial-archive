using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Lighting.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Lighting.View
{
    /// <summary>
    /// 区域灯光表现：美术预置的“灯光”叠层对象。区域未点亮时隐藏，
    /// 点亮后显示；只根据 LightingSystem 的状态与事件工作。
    /// </summary>
    public sealed class LightRegionView : MonoBehaviour
    {
        [SerializeField] private string regionId;

        private SpriteRenderer[] renderers;
        private EventBus boundEvents;
        private LightingSystem lightingSystem;
        private bool isBound;

        private void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            // 初始按黑暗处理，绑定系统后应用真实状态。
            ApplyVisibility(false);
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void Update()
        {
            if (!isBound)
            {
                TryBind();
            }
        }

        private void OnDisable()
        {
            if (boundEvents != null)
            {
                boundEvents.Unsubscribe<RegionLightsStateChangedEvent>(HandleRegionStateChanged);
                boundEvents = null;
            }

            isBound = false;
            lightingSystem = null;
        }

        private void TryBind()
        {
            if (isBound)
            {
                return;
            }

            var root = GameRoot.Instance;
            if (root == null || root.Context == null)
            {
                return;
            }

            lightingSystem = root.GetSystem<LightingSystem>();
            boundEvents = root.Context.Events;
            boundEvents.Subscribe<RegionLightsStateChangedEvent>(HandleRegionStateChanged);
            isBound = true;

            ApplyVisibility(lightingSystem != null && lightingSystem.IsRegionLit(regionId));
        }

        private void HandleRegionStateChanged(RegionLightsStateChangedEvent evt)
        {
            if (evt.RegionId == regionId)
            {
                ApplyVisibility(evt.IsLit);
            }
        }

        private void ApplyVisibility(bool isVisible)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (var spriteRenderer in renderers)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = isVisible;
                }
            }
        }
    }
}
