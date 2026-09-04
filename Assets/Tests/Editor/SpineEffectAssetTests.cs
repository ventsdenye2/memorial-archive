using NUnit.Framework;
using Spine.Unity;
using UnityEngine;

namespace MemorialArchive.Tests.Editor
{
    public sealed class SpineEffectAssetTests
    {
        [TestCase("SpineEffects/PlayerHurt/hurt_Effect_SkeletonData", "hurt")]
        [TestCase("SpineEffects/PlayerHurt/hurt_Effect_SkeletonData", "gangjiaban hurt")]
        [TestCase("SpineEffects/ShieldBlock/gedangchenggong_SkeletonData", "animation")]
        [TestCase("SpineEffects/GrenadeExplosion/explode_SkeletonData", "idle")]
        [TestCase("SpineEffects/PlayerBullet/dandao_SkeletonData", "animation")]
        [TestCase("SpineEffects/EnemyKnifeProjectile/Enemy_02_shoushudao dandao_SkeletonData", "animation")]
        [TestCase("SpineEffects/Buff/buff_SkeletonData", "animation")]
        [TestCase("SpineEffects/Heal/treat_SkeletonData", "animation")]
        public void AuthoredEffect_IsLoadableAndContainsAnimation(string resourcePath, string animationName)
        {
            var asset = Resources.Load<SkeletonDataAsset>(resourcePath);
            Assert.That(asset, Is.Not.Null, $"Missing Spine resource: {resourcePath}");

            var skeletonData = asset.GetSkeletonData(false);
            Assert.That(skeletonData, Is.Not.Null, $"Could not parse Spine data: {resourcePath}");
            Assert.That(skeletonData.FindAnimation(animationName), Is.Not.Null,
                $"Animation '{animationName}' was not found in {resourcePath}");
        }
    }
}
