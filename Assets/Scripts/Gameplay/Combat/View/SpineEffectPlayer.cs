using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace MemorialArchive.Gameplay.Combat.View
{
    /// <summary>
    /// Runtime entry point for authored one-shot Spine effects. Effect exports
    /// live under Resources so combat Views do not need scene or prefab wiring.
    /// </summary>
    public static class SpineEffectPlayer
    {
        public const string PlayerHurtResource = "SpineEffects/PlayerHurt/hurt_Effect_SkeletonData";
        public const string ShieldBlockResource = "SpineEffects/ShieldBlock/gedangchenggong_SkeletonData";
        public const string GrenadeExplosionResource = "SpineEffects/GrenadeExplosion/explode_SkeletonData";
        public const string PlayerBulletResource = "SpineEffects/PlayerBullet/dandao_SkeletonData";
        public const string EnemyKnifeProjectileResource = "SpineEffects/EnemyKnifeProjectile/Enemy_02_shoushudao dandao_SkeletonData";
        public const string BuffResource = "SpineEffects/Buff/buff_SkeletonData";
        public const string HealResource = "SpineEffects/Heal/treat_SkeletonData";

        private static readonly Dictionary<string, SkeletonDataAsset> LoadedAssets =
            new Dictionary<string, SkeletonDataAsset>();
        private static readonly HashSet<string> ReportedMissingAssets = new HashSet<string>();

        public static bool TryPlayAt(
            string resourcePath,
            string animationName,
            Vector3 worldPosition,
            float rotationDegrees = 0f,
            float uniformScale = 1f,
            int sortingOrder = 50,
            float maximumLifetimeSeconds = 0f,
            bool reversePlayback = false,
            float reverseStartTimeSeconds = -1f)
        {
            return TryPlay(
                resourcePath,
                animationName,
                worldPosition,
                rotationDegrees,
                uniformScale,
                sortingOrder,
                maximumLifetimeSeconds,
                reversePlayback,
                reverseStartTimeSeconds,
                null,
                Vector3.zero);
        }

        public static bool TryPlayFollowing(
            string resourcePath,
            string animationName,
            Transform followTarget,
            Vector3 localOffset,
            float uniformScale = 1f,
            int sortingOrder = 50,
            float maximumLifetimeSeconds = 0f)
        {
            if (followTarget == null)
            {
                return false;
            }

            return TryPlay(
                resourcePath,
                animationName,
                followTarget.TransformPoint(localOffset),
                0f,
                uniformScale,
                sortingOrder,
                maximumLifetimeSeconds,
                false,
                -1f,
                followTarget,
                localOffset);
        }

        private static bool TryPlay(
            string resourcePath,
            string animationName,
            Vector3 worldPosition,
            float rotationDegrees,
            float uniformScale,
            int sortingOrder,
            float maximumLifetimeSeconds,
            bool reversePlayback,
            float reverseStartTimeSeconds,
            Transform followTarget,
            Vector3 followOffset)
        {
            var skeletonData = Load(resourcePath);
            if (skeletonData == null)
            {
                return false;
            }

            var effectObject = new GameObject($"SpineEffect_{skeletonData.name}");
            effectObject.transform.SetPositionAndRotation(
                worldPosition,
                Quaternion.Euler(0f, 0f, rotationDegrees));
            effectObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, uniformScale);

            var skeletonAnimation = SkeletonAnimation.AddToGameObject(effectObject, skeletonData);
            if (skeletonAnimation == null || !skeletonAnimation.valid || skeletonAnimation.state == null)
            {
                Object.Destroy(effectObject);
                Debug.LogWarning($"Unable to initialize Spine effect '{resourcePath}'.");
                return false;
            }

            var trackEntry = skeletonAnimation.state.SetAnimation(0, animationName, false);
            if (trackEntry == null)
            {
                Object.Destroy(effectObject);
                Debug.LogWarning($"Spine effect '{resourcePath}' has no animation named '{animationName}'.");
                return false;
            }

            if (reversePlayback)
            {
                ConfigureReversePlayback(trackEntry, reverseStartTimeSeconds);
            }

            var meshRenderer = effectObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = sortingOrder;
            }

            var animationSeconds = Mathf.Max(0.05f, trackEntry.AnimationEnd - trackEntry.AnimationStart);
            var lifetimeSeconds = maximumLifetimeSeconds > 0f
                ? Mathf.Min(animationSeconds, maximumLifetimeSeconds)
                : animationSeconds;
            effectObject.AddComponent<SpineEffectLifetime>().Initialize(
                lifetimeSeconds,
                followTarget,
                followOffset,
                trackEntry,
                reversePlayback);
            return true;
        }

        private static void ConfigureReversePlayback(TrackEntry trackEntry, float reverseStartTimeSeconds)
        {
            if (trackEntry == null)
            {
                return;
            }

            var animationDuration = Mathf.Max(0f, trackEntry.AnimationEnd - trackEntry.AnimationStart);
            var reverseStartTime = reverseStartTimeSeconds >= 0f
                ? Mathf.Min(animationDuration, reverseStartTimeSeconds)
                : animationDuration;
            trackEntry.TrackTime = Mathf.Max(0f, reverseStartTime);
            // Spine 3.8 has no TrackEntry.Reverse API. A negative entry time
            // scale is the runtime-supported way to walk TrackTime backwards;
            // setting AnimationLast to the sampled start prevents a false
            // completion on the first apply. dandao's useful bone motion is
            // in its first 0.1667 seconds; callers can therefore pass a short
            // reverse window instead of traversing its later looping frames.
            // Its attachment/deform timelines are evaluated at each
            // descending AnimationTime normally.
            var timeScale = Mathf.Abs(trackEntry.TimeScale);
            trackEntry.TimeScale = -(timeScale > 0.0001f ? timeScale : 1f);
            trackEntry.AnimationLast = Mathf.Min(
                trackEntry.AnimationStart + trackEntry.TrackTime,
                trackEntry.AnimationEnd);
        }

        private static SkeletonDataAsset Load(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            if (LoadedAssets.TryGetValue(resourcePath, out var cached))
            {
                return cached;
            }

            var loaded = Resources.Load<SkeletonDataAsset>(resourcePath);
            if (loaded != null)
            {
                LoadedAssets.Add(resourcePath, loaded);
                return loaded;
            }

            if (ReportedMissingAssets.Add(resourcePath))
            {
                Debug.LogWarning(
                    $"Missing generated Spine SkeletonData asset at Resources/{resourcePath}. " +
                    "Refresh the Unity Asset Database so the JSON and Atlas are imported.");
            }
            return null;
        }
    }

    /// <summary>Keeps an optional character-bound effect aligned and destroys it after one shot.</summary>
    internal sealed class SpineEffectLifetime : MonoBehaviour
    {
        private float remainingSeconds;
        private Transform followTarget;
        private Vector3 followOffset;

        public void Initialize(
            float lifetimeSeconds,
            Transform target,
            Vector3 localOffset,
            TrackEntry playbackTrackEntry = null,
            bool reversePlayback = false)
        {
            remainingSeconds = Mathf.Max(0.05f, lifetimeSeconds);
            followTarget = target;
            followOffset = localOffset;
            this.playbackTrackEntry = playbackTrackEntry;
            this.reversePlayback = reversePlayback;
            ApplyFollowPosition();
        }

        private TrackEntry playbackTrackEntry;
        private bool reversePlayback;

        private void LateUpdate()
        {
            ApplyFollowPosition();

            if (reversePlayback && playbackTrackEntry != null)
            {
                if (playbackTrackEntry.Animation == null ||
                    float.IsNaN(playbackTrackEntry.TrackTime) ||
                    float.IsInfinity(playbackTrackEntry.TrackTime))
                {
                    // The owner cleared/disposed the private effect state.
                    // Do not retain a detached GameObject or a stale pooled
                    // TrackEntry reference.
                    Destroy(gameObject);
                    return;
                }

                if (playbackTrackEntry.TrackTime <= 0f)
                {
                    // Clamp the entry at its authored start before destroy so
                    // it never remains in a negative-time/setup-pose state.
                    // TrackTime is the lifetime source of truth here, so a
                    // paused SkeletonAnimation/timeScale does not destroy a
                    // still-visible reverse effect prematurely.
                    playbackTrackEntry.TrackTime = 0f;
                    Destroy(gameObject);
                }
                return;
            }

            remainingSeconds -= Time.deltaTime;
            if (remainingSeconds <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void ApplyFollowPosition()
        {
            if (followTarget != null)
            {
                transform.position = followTarget.TransformPoint(followOffset);
            }
        }
    }
}
