using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Lighting.Config;
using MemorialArchive.Gameplay.Lighting.Data;
using MemorialArchive.Gameplay.Lighting.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Lighting.View
{
    /// <summary>
    /// 灯具表现：向 LightingSystem 注册位置/半径/区域信息，并按
    /// LightStateChangedEvent 切换亮灭外观。本身不做任何规则判断。
    /// 交互由同物体上的 InteractionPointView(F) 走交互系统。
    /// </summary>
    public sealed class LightSourceView : MonoBehaviour
    {
        [SerializeField] private string lightId;
        [SerializeField] private Color litColor = new Color(1f, 0.85f, 0.45f, 1f);
        [SerializeField] private Color offColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        private SpriteRenderer spriteRenderer;
        private EventBus boundEvents;
        private LightingSystem lightingSystem;
        private bool isRegistered;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyState(false);
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void Update()
        {
            if (!isRegistered)
            {
                TryBind();
            }
        }

        private void OnDisable()
        {
            if (boundEvents != null)
            {
                boundEvents.Unsubscribe<LightStateChangedEvent>(HandleLightStateChanged);
                boundEvents = null;
            }

            if (isRegistered)
            {
                lightingSystem?.UnregisterLightView(lightId);
                isRegistered = false;
            }

            lightingSystem = null;
        }

        private void TryBind()
        {
            var root = GameRoot.Instance;
            if (root == null || root.Context == null)
            {
                return;
            }

            if (!isRegistered)
            {
                var lightConfig = root.Context.Configs.GetLightSource(lightId);
                if (lightConfig == null)
                {
                    // 配置缺失时不注册，Update 会持续重试，方便发现摆灯遗漏。
                    return;
                }

                lightingSystem = root.GetSystem<LightingSystem>();
                if (lightingSystem == null)
                {
                    return;
                }

                lightingSystem.RegisterLightView(new LightViewRegistration
                {
                    lightId = lightId,
                    regionId = lightConfig.RegionId,
                    isSpecial = lightConfig.IsSpecial,
                    position = transform.position,
                    radius = lightConfig.Radius,
                    sceneName = gameObject.scene.name
                });
                isRegistered = true;

                boundEvents = root.Context.Events;
                boundEvents.Subscribe<LightStateChangedEvent>(HandleLightStateChanged);
                ApplyState(lightingSystem.IsLightOn(lightId));
            }
        }

        private void HandleLightStateChanged(LightStateChangedEvent evt)
        {
            if (evt.LightId == lightId)
            {
                ApplyState(evt.IsOn);
            }
        }

        private void ApplyState(bool isOn)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = isOn ? litColor : offColor;
            }
        }
    }
}
