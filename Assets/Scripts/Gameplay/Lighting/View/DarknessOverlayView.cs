using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Gameplay.Lighting.Config;
using MemorialArchive.Gameplay.Lighting.Data;
using MemorialArchive.Gameplay.Lighting.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Lighting.View
{
    /// <summary>
    /// 全屏黑暗层：跟随主相机的大面片 + DarknessOverlay shader。
    /// 每帧从 LightingSystem 拉取暗幕开关与活跃光源，不持有任何规则。
    /// </summary>
    public sealed class DarknessOverlayView : MonoBehaviour
    {
        [SerializeField] private LightingGlobalConfig config;
        [SerializeField] private int sortingOrder = 50;
        [SerializeField] private float quadSize = 300f;

        private readonly List<ActiveLight> activeLights = new List<ActiveLight>();
        private readonly Vector4[] lightData = new Vector4[32];
        private MaterialPropertyBlock propertyBlock;
        private MeshRenderer quadRenderer;
        private Transform cameraTransform;
        private UnityEngine.Camera viewCamera;
        private LightingSystem lightingSystem;
        private float dimFactor = 1f;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            BuildQuad();
        }

        private void LateUpdate()
        {
            if (quadRenderer == null)
            {
                return;
            }

            ResolveDependencies();
            if (lightingSystem == null)
            {
                quadRenderer.enabled = false;
                return;
            }

            FollowCamera();

            if (lightingSystem.IsDarknessOverlaySuppressed)
            {
                // F3 调试模式需要立即露出完整场景，避免暗幕淡出期间仍然看不清素材。
                dimFactor = 0f;
            }
            else
            {
                var targetDim = lightingSystem.IsCurrentSceneLit() ? 0f : 1f;
                var fadePerSecond = config != null ? 1f / config.DarknessFadeSeconds : 2.5f;
                dimFactor = Mathf.MoveTowards(dimFactor, targetDim, Time.deltaTime * fadePerSecond);
            }
            quadRenderer.enabled = dimFactor > 0.001f;

            quadRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat("_Dim", dimFactor);
            if (config != null)
            {
                propertyBlock.SetColor("_DarknessColor", config.DarknessColor);
                propertyBlock.SetFloat("_DarknessAlpha", config.DarknessAlpha);
                propertyBlock.SetFloat("_FalloffExponent", config.LightFalloffExponent);
            }

            lightingSystem.CollectActiveLights(activeLights);
            var halfHeight = viewCamera != null ? viewCamera.orthographicSize : quadSize * .5f;
            var halfSize = new Vector2(halfHeight * (viewCamera != null ? viewCamera.aspect : 1), halfHeight);
            var lightCount = LightRenderSelection.Fill(activeLights, transform.position, halfSize, lightData,
                config != null ? config.MaxSimultaneousLights : lightData.Length);

            propertyBlock.SetVectorArray("_LightData", lightData);
            propertyBlock.SetInt("_LightCount", lightCount);
            quadRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ResolveDependencies()
        {
            if (lightingSystem == null)
            {
                lightingSystem = GameRoot.Instance?.GetSystem<LightingSystem>();
            }

            if (cameraTransform == null)
            {
                // 项目内存在 MemorialArchive.Gameplay.Camera 命名空间，需完全限定。
                var mainCamera = UnityEngine.Camera.main;
                if (mainCamera != null)
                {
                    viewCamera = mainCamera;
                    cameraTransform = mainCamera.transform;
                }
            }
        }

        private void FollowCamera()
        {
            if (cameraTransform != null)
            {
                transform.position = new Vector3(cameraTransform.position.x, cameraTransform.position.y, -1f);
            }
        }

        private void BuildQuad()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "DarknessQuad";
            DestroyGetComponent<Collider>(quad);
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localScale = new Vector3(quadSize, quadSize, 1f);

            quadRenderer = quad.GetComponent<MeshRenderer>();
            quadRenderer.sortingOrder = sortingOrder;
            quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quadRenderer.receiveShadows = false;

            var material = config != null && config.DarknessMaterial != null
                ? config.DarknessMaterial
                : new Material(Shader.Find("Memorial Archive/Lighting/Darkness Overlay"));
            // 实例化一份材质，避免运行时改动共享资产。
            quadRenderer.sharedMaterial = Instantiate(material);
        }

        private static void DestroyGetComponent<TComponent>(GameObject target) where TComponent : Component
        {
            var component = target.GetComponent<TComponent>();
            if (component != null)
            {
                Destroy(component);
            }
        }
    }
}
