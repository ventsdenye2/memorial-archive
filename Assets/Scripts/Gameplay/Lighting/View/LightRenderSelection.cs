using System.Collections.Generic;
using MemorialArchive.Gameplay.Lighting.Data;
using UnityEngine;

namespace MemorialArchive.Gameplay.Lighting.View
{
    /// <summary>Allocates the shader's bounded light slots to lights affecting the current camera.</summary>
    public static class LightRenderSelection
    {
        public static int Fill(List<ActiveLight> lights, Vector2 cameraCenter, Vector2 cameraHalfSize, Vector4[] output, int limit)
        {
            int capacity = Mathf.Clamp(limit, 0, output.Length);
            int count = 0;
            foreach (var light in lights)
            {
                if (capacity == 0 || light.Radius <= 0 || light.Intensity <= 0) continue;
                var delta = light.Position - cameraCenter;
                var outside = new Vector2(Mathf.Max(0, Mathf.Abs(delta.x) - cameraHalfSize.x),
                    Mathf.Max(0, Mathf.Abs(delta.y) - cameraHalfSize.y));
                if (outside.sqrMagnitude > light.Radius * light.Radius) continue;

                var candidate = new Vector4(light.Position.x, light.Position.y, light.Radius, light.Intensity);
                float score = Score(candidate, cameraCenter);
                int insert = 0;
                while (insert < count && Score(output[insert], cameraCenter) <= score) insert++;
                if (insert >= capacity) continue;
                for (int i = Mathf.Min(count, capacity - 1); i > insert; i--) output[i] = output[i - 1];
                output[insert] = candidate;
                count = Mathf.Min(count + 1, capacity);
            }
            return count;
        }

        private static float Score(Vector4 light, Vector2 center) =>
            Vector2.Distance(new Vector2(light.x, light.y), center) - light.z;
    }
}
