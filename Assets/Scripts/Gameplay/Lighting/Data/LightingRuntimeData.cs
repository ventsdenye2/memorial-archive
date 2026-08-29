using System;
using System.Collections.Generic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Lighting.Data
{
    public enum LanternStage
    {
        Off = 0,
        Strong = 1,
        Normal = 2,
        Weak = 3
    }

    /// <summary>场景内灯具 View 注册到 LightingSystem 的信息。</summary>
    public sealed class LightViewRegistration
    {
        public string lightId;
        public string regionId;
        public bool isSpecial;
        public Vector2 position;
        public float radius;
        public string sceneName;
    }

    /// <summary>黑暗层每帧拉取的视觉光源（角色微光、手提灯与临时灯）。</summary>
    public struct ActiveLight
    {
        public ActiveLight(Vector2 position, float radius, float intensity = 1f)
        {
            Position = position;
            Radius = radius;
            Intensity = Mathf.Clamp01(intensity);
        }

        public Vector2 Position { get; }
        public float Radius { get; }
        public float Intensity { get; }
    }

    [Serializable]
    public sealed class LightingSaveData
    {
        public List<RegionSaveData> regions = new List<RegionSaveData>();
        public List<LanternFuelSaveData> lanternFuel = new List<LanternFuelSaveData>();
    }

    [Serializable]
    public sealed class RegionSaveData
    {
        public string regionId;
        public bool isLit;
    }

    [Serializable]
    public sealed class LanternFuelSaveData
    {
        public string instanceId;
        public float remainingSeconds;
    }
}
