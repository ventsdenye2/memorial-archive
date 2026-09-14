using System.Collections.Generic;
using MemorialArchive.Gameplay.Lighting.Data;
using MemorialArchive.Gameplay.Lighting.View;
using NUnit.Framework;
using UnityEngine;

namespace MemorialArchive.Tests.Editor
{
    public sealed class LightRenderSelectionTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void OverlappingLightColorsAreOrderIndependentAndContinuous(bool bothColored)
        {
            var first = new Vector4(1f, .72f, .34f, 1f);
            var second = bothColored ? new Vector4(.4f, .7f, 1f, 1f) : Vector4.one;
            var tied = new Vector4(0f, 0f, 4f, 1f);
            var forward = RenderOverlap(tied, tied, first, second);
            var reverse = RenderOverlap(tied, tied, second, first);
            for (var i = 0; i < forward.Length; i++)
                Assert.That(ColorDifference(forward[i], reverse[i]), Is.LessThan(.005f), "Light order changed pixel " + i);

            var crossing = RenderOverlap(new Vector4(-1f, 0f, 4f, 1f),
                new Vector4(1f, 0f, 4f, 1f), first, second);
            for (var x = 124; x < 132; x++)
                Assert.That(ColorDifference(crossing[64 * 257 + x], crossing[64 * 257 + x + 1]),
                    Is.LessThan(.025f), "Color seam at the equal-strength boundary");
        }

        private static float ColorDifference(Color a, Color b) =>
            Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));

        private static Color[] RenderOverlap(Vector4 first, Vector4 second, Vector4 firstColor, Vector4 secondColor)
        {
            var shader = Shader.Find("Memorial Archive/Lighting/Darkness Overlay");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
            var material = new Material(shader);
            var cameraObject = new GameObject("LightBlendTestCamera", typeof(Camera));
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var target = new RenderTexture(257, 129, 24);
            var pixels = new Texture2D(257, 129, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active;
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = 2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
                camera.cullingMask = 1 << 31;
                camera.targetTexture = target;
                quad.layer = 31;
                quad.transform.localScale = new Vector3(9f, 5f, 1f);
                quad.GetComponent<MeshRenderer>().sharedMaterial = material;
                var lights = new Vector4[32];
                var colors = new Vector4[32];
                lights[0] = first; lights[1] = second;
                colors[0] = firstColor; colors[1] = secondColor;
                material.SetVectorArray("_LightData", lights);
                material.SetVectorArray("_LightColorData", colors);
                material.SetInt("_LightCount", 2);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 257, 129), 0, 0);
                pixels.Apply();
                return pixels.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(quad);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(pixels);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void NearLightsRemainWithinCapacityWhenManyFartherLightsArePresent()
        {
            var lights = new List<ActiveLight>();
            for (var index = 0; index < 40; index++)
            {
                var angle = index * Mathf.PI * 2f / 40f;
                lights.Add(new ActiveLight(new Vector2(Mathf.Cos(angle) * 8f, Mathf.Sin(angle) * 8f), 0.5f));
            }

            var streetLight = new ActiveLight(new Vector2(1.5f, 0f), 2f, 0.7f);
            var lantern = new ActiveLight(new Vector2(-1f, 0f), 3f, 0.35f);
            lights.Add(streetLight);
            lights.Add(lantern);
            var original = new List<ActiveLight>(lights);
            var output = new Vector4[32];

            var count = LightRenderSelection.Fill(lights, Vector2.zero, new Vector2(10f, 10f), output, 32);

            Assert.That(count, Is.EqualTo(32));
            Assert.That(Contains(output, count, streetLight), Is.True);
            Assert.That(Contains(output, count, lantern), Is.True);
            Assert.That(lights, Is.EqualTo(original), "Selection must not reorder or mutate the input list.");
            lights.RemoveAt(lights.Count - 1);
            count = LightRenderSelection.Fill(lights, Vector2.zero, new Vector2(10f, 10f), output, 32);
            Assert.That(Contains(output, count, streetLight), Is.True, "Unequipping must not change fixture visibility.");
        }

        [Test]
        public void LightOutsideAabbIsKeptWhenItsRadiusReachesTheView()
        {
            var lights = new List<ActiveLight>
            {
                new ActiveLight(new Vector2(12f, 0f), 3f, 0.6f),
                new ActiveLight(new Vector2(12f, 0f), 1f, 0.4f)
            };
            var output = new Vector4[4];

            var count = LightRenderSelection.Fill(lights, Vector2.zero, new Vector2(10f, 10f), output, 4);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(output[0], Is.EqualTo(new Vector4(12f, 0f, 3f, 0.6f)));
        }

        [Test]
        public void LimitAndOutputCapacityAreBothRespected()
        {
            var lights = new List<ActiveLight>
            {
                new ActiveLight(Vector2.zero, 1f),
                new ActiveLight(Vector2.right, 1f),
                new ActiveLight(Vector2.left, 1f),
                new ActiveLight(Vector2.up, 1f)
            };
            var output = new Vector4[2];

            var count = LightRenderSelection.Fill(lights, Vector2.zero, new Vector2(10f, 10f), output, 3);

            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void ColorOutputFollowsLightWhenSelectionIsSorted()
        {
            var warm = new ActiveLight(Vector2.right * 3f, 2f, 1.35f, new Color(1f, .72f, .34f));
            var white = new ActiveLight(Vector2.zero, 1f, 1f, Color.white);
            var lights = new List<ActiveLight> { warm, white };
            var output = new Vector4[2];
            var colors = new Vector4[2];

            var count = LightRenderSelection.Fill(lights, Vector2.zero, new Vector2(10f, 10f), output, colors, 2);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(colors[0], Is.EqualTo((Vector4)Color.white));
            Assert.That(colors[1], Is.EqualTo((Vector4)warm.Color));
        }

        [Test]
        public void InvalidRadiusIsDiscarded()
        {
            var lights = new List<ActiveLight>
            {
                new ActiveLight(),
                new ActiveLight(Vector2.zero, 1f)
            };
            var output = new Vector4[4];

            var count = LightRenderSelection.Fill(lights, Vector2.zero, new Vector2(10f, 10f), output, 4);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(output[0].z, Is.EqualTo(1f));
        }

        private static bool Contains(Vector4[] output, int count, ActiveLight expected)
        {
            var value = new Vector4(expected.Position.x, expected.Position.y, expected.Radius, expected.Intensity);
            for (var index = 0; index < count; index++)
                if (output[index] == value) return true;
            return false;
        }
    }
}
