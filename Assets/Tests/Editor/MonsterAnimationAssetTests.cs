using MemorialArchive.Gameplay.Monster.Data;
using MemorialArchive.Gameplay.Monster.View;
using NUnit.Framework;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

namespace MemorialArchive.Tests.Editor
{
    public sealed class MonsterAnimationAssetTests
    {
        private GameObject visual;
        private SkeletonAnimation skeleton;
        private MonsterAnimationView view;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monster/Stage1MeleeMonster.prefab");
            var source = prefab.GetComponentInChildren<SkeletonAnimation>();
            skeleton = SkeletonAnimation.NewSkeletonAnimationGameObject(source.skeletonDataAsset);
            visual = skeleton.gameObject;
            view = visual.AddComponent<MonsterAnimationView>();
            EditorUtility.CopySerialized(prefab.GetComponent<MonsterAnimationView>(), view);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("skeletonAnimation").objectReferenceValue = skeleton;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            view.UpdateHealth(1500f, 1500f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(visual);
        }

        [Test]
        public void HealthThreshold_ChangesSkinWithoutRestartingAttack_AndResetsOnRespawn()
        {
            view.PlayState(MonsterActionState.Attacking);
            var track = skeleton.AnimationState.GetCurrent(0);
            view.UpdateHealth(751f, 1500f);
            Assert.That(skeleton.Skeleton.Skin.Name, Is.EqualTo("normal"));
            view.UpdateHealth(750f, 1500f);
            Assert.That(skeleton.Skeleton.Skin.Name, Is.EqualTo("damage"));
            Assert.That(skeleton.AnimationState.GetCurrent(0), Is.SameAs(track));
            view.UpdateHealth(0f, 1500f);
            Assert.That(skeleton.Skeleton.Skin.Name, Is.EqualTo("damage"));
            view.UpdateHealth(1500f, 1500f);
            Assert.That(skeleton.Skeleton.Skin.Name, Is.EqualTo("normal"));
        }

        [TestCase("Enemy_01_attack01", 0.3667f)]
        [TestCase("Enemy_01_attack02", 0.4667f)]
        public void AuthoredAttack_ReportsExactlyOneHitAtItsEvent(string animation, float hitTime)
        {
            var serialized = new SerializedObject(view);
            serialized.FindProperty("attackAnimation").stringValue = animation;
            serialized.FindProperty("secondAttackAnimation").stringValue = string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var hits = 0;
            view.AttackHit += () => hits++;
            view.PlayState(MonsterActionState.Attacking);
            skeleton.Update(hitTime - 0.02f);
            Assert.That(hits, Is.Zero);
            skeleton.Update(0.04f);
            Assert.That(hits, Is.EqualTo(1));
            skeleton.Update(2f);
            Assert.That(hits, Is.EqualTo(1));
        }

        [Test]
        public void Attacks_SelectBothVariants_AndHurtCancelsPendingHit()
        {
            var savedRandomState = Random.state;
            try
            {
                Random.InitState(1234);
                var names = new System.Collections.Generic.HashSet<string>();
                for (var index = 0; index < 40; index++)
                {
                    view.PlayState(MonsterActionState.Idle);
                    view.PlayState(MonsterActionState.Attacking);
                    names.Add(skeleton.AnimationState.GetCurrent(0).Animation.Name);
                }
                Assert.That(names, Is.EquivalentTo(new[] { "Enemy_01_attack01", "Enemy_01_attack02" }));
                var hits = 0;
                view.AttackHit += () => hits++;
                view.PlayState(MonsterActionState.Hurt);
                Assert.That(skeleton.AnimationState.GetCurrent(0).Animation.Name, Is.EqualTo("Enemy_01_hurt"));
                Assert.That(view.CurrentAnimationDuration, Is.GreaterThan(0f));
                skeleton.Update(1f);
                Assert.That(hits, Is.Zero);
            }
            finally
            {
                Random.state = savedRandomState;
            }
        }
    }
}
