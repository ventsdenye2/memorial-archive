using System.Collections.Generic;
using MemorialArchive.Gameplay.Lighting.Data;
using MemorialArchive.Gameplay.Lighting.View;
using NUnit.Framework;
using UnityEngine;

namespace MemorialArchive.Tests.Editor
{
    public sealed class LightRenderSelectionTests
    {
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
