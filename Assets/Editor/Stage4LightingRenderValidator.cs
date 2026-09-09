#if UNITY_EDITOR
using System;
using MemorialArchive.Gameplay.Lighting.Config;
using UnityEditor;
using UnityEngine;

namespace MemorialArchive.Editor
{
    /// <summary>
    /// 光照渲染验证：确认 DarknessOverlay shader 可编译、材质资产可用，
    /// 并离屏渲染一帧带光源的暗幕，读像素断言“远处变黑、光源中心透亮”。
    /// 菜单运行或 batchmode：-executeMethod MemorialArchive.Editor.Stage4LightingRenderValidator.ValidateBatch
    /// </summary>
    public static class Stage4LightingRenderValidator
    {
        private const string ShaderName = "Memorial Archive/Lighting/Darkness Overlay";
        private const string MaterialPath = "Assets/GameConfigs/Lighting/DarknessOverlayMaterial.mat";
        private const string GlobalConfigPath = "Assets/GameConfigs/Lighting/LightingGlobalConfig.asset";
        private const int RenderSize = 256;
        private const int ValidationLayer = 31;

        [MenuItem("Tools/Memorial Archive/Validate Stage 4 Lighting Render")]
        public static void Validate()
        {
            ValidateShaderCompiles();
            var material = ValidateMaterialAsset();
            ValidateGlobalConfigMaterial(material);
            ValidateRenderOutput(material);
            Debug.Log("[Stage4LightingRenderValidator] 渲染验证通过：shader 可编译，材质可用，暗幕与光源挖洞像素符合预期。");
        }

        public static void ValidateBatch()
        {
            try
            {
                Validate();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidateShaderCompiles()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException($"找不到 shader：{ShaderName}");
            }

            if (ShaderUtil.ShaderHasError(shader))
            {
                var details = new System.Text.StringBuilder();
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                {
                    details.AppendLine($"{message.severity}: {message.message} (line {message.line})");
                }

                throw new InvalidOperationException($"shader 编译失败：{ShaderName}\n{details}");
            }

            if (!shader.isSupported)
            {
                throw new InvalidOperationException($"shader 在当前图形 API 上不受支持：{ShaderName}");
            }

            Debug.Log("[Stage4LightingRenderValidator] shader 编译检查通过。");
        }

        private static Material ValidateMaterialAsset()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                throw new InvalidOperationException($"找不到材质资产：{MaterialPath}");
            }

            if (material.shader == null || material.shader.name != ShaderName)
            {
                throw new InvalidOperationException($"材质资产未引用目标 shader：{MaterialPath} -> {material.shader?.name}");
            }

            return material;
        }

        private static void ValidateGlobalConfigMaterial(Material material)
        {
            var config = AssetDatabase.LoadAssetAtPath<LightingGlobalConfig>(GlobalConfigPath);
            if (config == null)
            {
                throw new InvalidOperationException($"找不到配置资产：{GlobalConfigPath}");
            }

            if (config.DarknessMaterial == null)
            {
                throw new InvalidOperationException("LightingGlobalConfig.darknessMaterial 未赋值。");
            }

            if (config.PlayerSafetyLightWidth <= 0f || config.PlayerSafetyLightHeight <= 0f ||
                config.PlayerSafetyLightIntensity <= 0f ||
                config.PlayerSafetyLightIntensity >= 1f)
            {
                throw new InvalidOperationException(
                    $"角色微光配置无效：width={config.PlayerSafetyLightWidth}, height={config.PlayerSafetyLightHeight}, " +
                    $"intensity={config.PlayerSafetyLightIntensity}");
            }

            Debug.Log("[Stage4LightingRenderValidator] 材质与全局配置引用检查通过。");
        }

        private static void ValidateRenderOutput(Material material)
        {
            var cameraObject = new GameObject("ValidationCamera");
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5.4f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                // 只渲染验证用四边形，避免当前打开场景里的 Sprite/Canvas 干扰离屏取样。
                camera.cullingMask = 1 << ValidationLayer;

                var collider = quad.GetComponent<Collider>();
                if (collider != null)
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }

                quad.transform.position = Vector3.zero;
                quad.transform.localScale = new Vector3(40f, 40f, 1f);
                quad.layer = ValidationLayer;
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingOrder = 50;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var propertyBlock = new MaterialPropertyBlock();
                propertyBlock.SetColor("_DarknessColor", Color.black);
                propertyBlock.SetFloat("_DarknessAlpha", 0.95f);
                propertyBlock.SetFloat("_FalloffExponent", 2f);
                propertyBlock.SetFloat("_Dim", 1f);
                var lightData = new Vector4[32];
                var lightShapeData = new Vector4[32];
                lightData[0] = new Vector4(0f, 0f, 2f, 1f);
                propertyBlock.SetVectorArray("_LightData", lightData);
                propertyBlock.SetVectorArray("_LightShapeData", lightShapeData);
                propertyBlock.SetInt("_LightCount", 1);
                renderer.SetPropertyBlock(propertyBlock);

                var texture = new RenderTexture(RenderSize, RenderSize, 0, RenderTextureFormat.ARGB32);
                camera.targetTexture = texture;
                camera.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = texture;
                var pixels = ReadAllPixels();

                var center = SamplePixel(pixels, RenderSize / 2, RenderSize / 2);
                var corner = SamplePixel(pixels, 8, 8);

                var config = AssetDatabase.LoadAssetAtPath<LightingGlobalConfig>(GlobalConfigPath);
                lightData[0] = new Vector4(
                    0f,
                    0f,
                    Mathf.Max(config.PlayerSafetyLightWidth, config.PlayerSafetyLightHeight) * 0.5f,
                    config.PlayerSafetyLightIntensity);
                lightShapeData[0] = new Vector4(
                    1f,
                    config.PlayerSafetyLightWidth * 0.5f,
                    config.PlayerSafetyLightHeight * 0.5f,
                    0f);
                propertyBlock.SetVectorArray("_LightData", lightData);
                propertyBlock.SetVectorArray("_LightShapeData", lightShapeData);
                renderer.SetPropertyBlock(propertyBlock);
                camera.Render();

                RenderTexture.active = texture;
                var weakPixels = ReadAllPixels();
                var weakCenter = SamplePixel(weakPixels, RenderSize / 2, RenderSize / 2);
                var weakCorner = SamplePixel(weakPixels, 8, 8);
                var fullBrightest = FindBrightestPixel(pixels, out var fullBrightestX, out var fullBrightestY);
                var weakBrightest = FindBrightestPixel(weakPixels, out var weakBrightestX, out var weakBrightestY);
                RenderTexture.active = previous;
                camera.targetTexture = null;
                texture.Release();

                Debug.Log(
                    $"[Stage4LightingRenderValidator] 取样诊断：full center={center.r}, max={fullBrightest.r}@({fullBrightestX},{fullBrightestY}); " +
                    $"weak center={weakCenter.r}, max={weakBrightest.r}@({weakBrightestX},{weakBrightestY}); far={corner.r}。");

                // 光源中心（半径 2、强度 1）应完全透出白色背景；远处应为白底叠 95% 黑。
                if (center.r < 200)
                {
                    throw new InvalidOperationException($"光源中心像素过暗，挖洞未生效：r={center.r}");
                }

                if (corner.r > 60)
                {
                    throw new InvalidOperationException($"远离光源的像素过亮，暗幕未生效：r={corner.r}");
                }

                if (weakCenter.r <= weakCorner.r + 45)
                {
                    throw new InvalidOperationException(
                        $"角色微光中心仍不可辨认：center={weakCenter.r}, corner={weakCorner.r}");
                }

                if (weakCenter.r >= center.r - 20)
                {
                    throw new InvalidOperationException(
                        $"角色微光过亮，与完整光源缺少层级：weak={weakCenter.r}, full={center.r}");
                }

                Debug.Log(
                    $"[Stage4LightingRenderValidator] 像素断言通过：完整光源 r={center.r}，角色微光 r={weakCenter.r}，远处 r={corner.r}。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(quad);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static Color32[] ReadAllPixels()
        {
            var readback = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0, 0, RenderSize, RenderSize), 0, 0);
            readback.Apply();
            var pixels = readback.GetPixels32();
            UnityEngine.Object.DestroyImmediate(readback);
            return pixels;
        }

        private static Color32 SamplePixel(Color32[] pixels, int x, int y)
        {
            return pixels[y * RenderSize + x];
        }

        private static Color32 FindBrightestPixel(Color32[] pixels, out int x, out int y)
        {
            var brightestIndex = 0;
            for (var index = 1; index < pixels.Length; index++)
            {
                if (pixels[index].r > pixels[brightestIndex].r)
                {
                    brightestIndex = index;
                }
            }

            x = brightestIndex % RenderSize;
            y = brightestIndex / RenderSize;
            return pixels[brightestIndex];
        }
    }
}
#endif
