using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Character.Data;
using MemorialArchive.Gameplay.Character.Logic;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Combat.Logic;
using MemorialArchive.Gameplay.Item.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MemorialArchive.Tests.Editor
{
    public sealed class CharacterStateMachineTests
    {
        private const int FireAxeItemId = 1003;
        private const int GrenadeItemId = 1010;
        [TestCase(1001)]
        [TestCase(1003)]
        [TestCase(1007)]
        [TestCase(1008)]
        [TestCase(1010)]
        public void Running_PrimaryGestureDoesNotAttackOrSpendStamina(int itemId)
        {
            var character = CreateCharacterSystem(out var events);
            var attacks = 0;
            events.Subscribe<CharacterAttackRequestedEvent>(e => attacks++);
            events.Publish(new CharacterEquipmentChangedEvent(itemId, OffhandType.None));
            events.Publish(new MoveInputEvent(Vector2.right));
            events.Publish(new RunInputEvent(true));
            var stamina = character.Data.stamina;
            events.Publish(new SecondaryActionInputEvent(false, Vector2.right));
            events.Publish(new PrimaryActionPhaseEvent(PrimaryActionPhase.Started, Vector2.right));
            events.Publish(new PrimaryActionPressedEvent());
            events.Publish(new PrimaryActionPhaseEvent(PrimaryActionPhase.Released, Vector2.right));
            Assert.That(attacks, Is.Zero);
            Assert.That(character.Data.stamina, Is.EqualTo(stamina));
            Assert.That(character.IsRunning, Is.True);
            character.Dispose();
        }
        [Test]
        public void EquipAnimation_BlocksRunningOnlyWhileAnimationIsPlaying()
        {
            var character = CreateCharacterSystem(out var events);
            events.Publish(new MoveInputEvent(Vector2.right));
            Assert.That(character.MoveDirection, Is.EqualTo(Vector2.right));

            events.Publish(new CharacterEquipmentChangedEvent(FireAxeItemId, OffhandType.None));
            events.Publish(new RunInputEvent(true));
            Assert.That(character.IsRunning, Is.True, "An equipped weapon must not permanently disable running.");
            events.Publish(new MoveInputEvent(Vector2.left));
            Assert.That(character.MoveDirection, Is.EqualTo(Vector2.left), "Holding an equipped weapon must not block movement.");

            events.Publish(new CharacterEquipAnimationStateChangedEvent(true));
            events.Publish(new RunInputEvent(true));
            events.Publish(new MoveInputEvent(Vector2.left));

            Assert.That(character.ActionState, Is.EqualTo(CharacterActionState.Equipping));
            Assert.That(character.MoveDirection, Is.EqualTo(Vector2.left), "Equip animation may continue walking, but must not allow running.");
            Assert.That(character.IsRunning, Is.False);

            events.Publish(new CharacterEquipAnimationStateChangedEvent(false));
            events.Publish(new MoveInputEvent(Vector2.right));
            events.Publish(new RunInputEvent(true));
            Assert.That(character.ActionState, Is.EqualTo(CharacterActionState.Normal));
            Assert.That(character.MoveDirection, Is.EqualTo(Vector2.right));
            Assert.That(character.IsRunning, Is.True);
            character.Dispose();
        }

        [Test]
        public void PrimaryAction_BuffersOneComboAndStartsItDirectlyWhenCurrentAnimationCompletes()
        {
            var character = CreateCharacterSystem(out var events);
            var requestedComboStages = new System.Collections.Generic.List<int>();
            var publishedStates = new System.Collections.Generic.List<CharacterActionState>();
            events.Subscribe<CharacterAttackRequestedEvent>(evt => requestedComboStages.Add(evt.ComboStage));
            events.Subscribe<CharacterActionStateChangedEvent>(evt => publishedStates.Add(evt.State));
            events.Publish(new CharacterEquipmentChangedEvent(FireAxeItemId, OffhandType.None));

            events.Publish(new PrimaryActionPressedEvent());
            var activeAttackState = character.ActionState;
            events.Publish(new PrimaryActionPressedEvent());
            events.Publish(new PrimaryActionPressedEvent());

            Assert.That(activeAttackState, Is.EqualTo(CharacterActionState.Attack1));
            Assert.That(character.ActionState, Is.EqualTo(activeAttackState));
            Assert.That(requestedComboStages, Is.EqualTo(new[] { 1 }));

            events.Publish(new CharacterActionAnimationCompletedEvent(activeAttackState));

            Assert.That(requestedComboStages, Is.EqualTo(new[] { 1, 2 }), "The chained attack must create its own combat request for hit detection and damage.");
            Assert.That(character.ActionState, Is.EqualTo(CharacterActionState.Attack2));
            Assert.That(publishedStates, Is.EqualTo(new[]
            {
                CharacterActionState.Attack1,
                CharacterActionState.Attack2
            }), "A buffered combo must transition directly without publishing an intermediate idle state.");
            character.Dispose();
        }

        [Test]
        public void PrimaryAction_ThirdComboCannotBufferAFourthAttack()
        {
            var character = CreateCharacterSystem(out var events);
            var requestedComboStages = new System.Collections.Generic.List<int>();
            var publishedStates = new System.Collections.Generic.List<CharacterActionState>();
            events.Subscribe<CharacterAttackRequestedEvent>(evt => requestedComboStages.Add(evt.ComboStage));
            events.Subscribe<CharacterActionStateChangedEvent>(evt => publishedStates.Add(evt.State));
            events.Publish(new CharacterEquipmentChangedEvent(FireAxeItemId, OffhandType.None));

            events.Publish(new PrimaryActionPressedEvent());
            events.Publish(new PrimaryActionPressedEvent());
            events.Publish(new CharacterActionAnimationCompletedEvent(CharacterActionState.Attack1));
            events.Publish(new PrimaryActionPressedEvent());
            events.Publish(new CharacterActionAnimationCompletedEvent(CharacterActionState.Attack2));
            events.Publish(new PrimaryActionPressedEvent());
            events.Publish(new CharacterActionAnimationCompletedEvent(CharacterActionState.Attack3));

            Assert.That(requestedComboStages, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(character.ActionState, Is.EqualTo(CharacterActionState.Normal));
            Assert.That(publishedStates, Is.EqualTo(new[]
            {
                CharacterActionState.Attack1,
                CharacterActionState.Attack2,
                CharacterActionState.Attack3,
                CharacterActionState.Normal
            }));
            character.Dispose();
        }

        [Test]
        public void BufferedCombo_IsDiscardedWhenAttackIsInterrupted()
        {
            var character = CreateCharacterSystem(out var events);
            var attackRequestCount = 0;
            events.Subscribe<CharacterAttackRequestedEvent>(_ => attackRequestCount++);
            events.Publish(new CharacterEquipmentChangedEvent(FireAxeItemId, OffhandType.None));

            events.Publish(new PrimaryActionPressedEvent());
            events.Publish(new PrimaryActionPressedEvent());
            events.Publish(new CharacterEquipAnimationStateChangedEvent(true));
            events.Publish(new CharacterActionAnimationCompletedEvent(CharacterActionState.Attack1));

            Assert.That(attackRequestCount, Is.EqualTo(1));
            Assert.That(character.ActionState, Is.EqualTo(CharacterActionState.Equipping));
            character.Dispose();
        }

        [Test]
        public void SecondaryAction_AllowsBlockingWithoutEquipment()
        {
            var character = CreateCharacterSystem(out var events);
            var blockPublished = false;
            events.Subscribe<BlockInputEvent>(evt => blockPublished = evt.IsBlocking);

            events.Publish(new SecondaryActionInputEvent(true, Vector2.zero));

            Assert.That(character.IsBlocking, Is.True);
            Assert.That(character.ActionState, Is.EqualTo(CharacterActionState.Blocking));
            Assert.That(blockPublished, Is.True);
            character.Dispose();
        }

        [Test]
        public void EmptyHandBlock_ReducesDamageToHalfAndConsumesFiveStaminaWithoutStagger()
        {
            var character = CreateCharacterSystem(out var events);
            var combat = new CombatSystem(character);
            combat.Initialize(CreateContext(events));
            events.Publish(new SecondaryActionInputEvent(true, Vector2.zero));

            var result = combat.ResolveDamage(new DamageRequest(
                0, "monster", CombatTargetIds.Player, 0, DamageType.Physical, 1f, Vector2.zero));

            Assert.That(result.WasBlocked, Is.True);
            Assert.That(result.FinalDamage, Is.EqualTo(0.5f));
            Assert.That(character.Data.health, Is.EqualTo(2.5f));
            Assert.That(character.Data.stamina, Is.EqualTo(25));
            Assert.That(character.ActionState, Is.EqualTo(CharacterActionState.Blocking));
            combat.Dispose();
            character.Dispose();
        }

        [Test]
        public void ShieldBlock_NegatesDamageAndConsumesFiveStamina()
        {
            var character = CreateCharacterSystem(out var events);
            var combat = new CombatSystem(character);
            combat.Initialize(CreateContext(events));
            events.Publish(new CharacterEquipmentChangedEvent(0, OffhandType.Shield));
            events.Publish(new SecondaryActionInputEvent(true, Vector2.zero));

            var result = combat.ResolveDamage(new DamageRequest(
                0, "monster", CombatTargetIds.Player, 0, DamageType.Physical, 1f, Vector2.zero));

            Assert.That(result.WasBlocked, Is.True);
            Assert.That(result.FinalDamage, Is.Zero);
            Assert.That(character.Data.health, Is.EqualTo(3f));
            Assert.That(character.Data.stamina, Is.EqualTo(25));
            Assert.That(character.ActionState, Is.EqualTo(CharacterActionState.Blocking));
            combat.Dispose();
            character.Dispose();
        }

        [Test]
        public void Blocking_ClearsMovementAndRejectsFurtherMoveInput()
        {
            var character = CreateCharacterSystem(out var events);
            events.Publish(new MoveInputEvent(Vector2.right));
            Assert.That(character.MoveDirection, Is.EqualTo(Vector2.right));

            events.Publish(new SecondaryActionInputEvent(true, Vector2.zero));
            events.Publish(new MoveInputEvent(Vector2.left));

            Assert.That(character.MoveDirection, Is.EqualTo(Vector2.zero));
            Assert.That(character.IsRunning, Is.False);
            character.Dispose();
        }

        [Test]
        public void Attacking_WalkingDoesNotInterruptAttack()
        {
            var character = CreateCharacterSystem(out var events);
            events.Publish(new CharacterEquipmentChangedEvent(FireAxeItemId, OffhandType.None));
            events.Publish(new MoveInputEvent(Vector2.right));

            events.Publish(new PrimaryActionPressedEvent());
            events.Publish(new MoveInputEvent(Vector2.left));

            Assert.That(character.ActionState, Is.EqualTo(CharacterActionState.Attack1));
            Assert.That(character.MoveDirection, Is.EqualTo(Vector2.zero));
            Assert.That(character.IsRunning, Is.False);
            character.Dispose();
        }

        [Test]
        public void Attacking_RunningInterruptsAttackAndResumesMovement()
        {
            var character = CreateCharacterSystem(out var events);
            events.Publish(new CharacterEquipmentChangedEvent(FireAxeItemId, OffhandType.None));
            events.Publish(new PrimaryActionPressedEvent());
            events.Publish(new MoveInputEvent(Vector2.left));
            events.Publish(new RunInputEvent(true));

            Assert.That(character.ActionState, Is.EqualTo(CharacterActionState.Normal));
            Assert.That(character.MoveDirection, Is.EqualTo(Vector2.left));
            Assert.That(character.IsRunning, Is.True);
            character.Dispose();
        }

        [Test]
        public void Throwable_HoldsToAimAndReleasesExactlyOneAttackWithoutMovement()
        {
            var character = CreateCharacterSystem(out var events);
            var attacks = new System.Collections.Generic.List<CharacterAttackRequestedEvent>();
            events.Subscribe<CharacterAttackRequestedEvent>(attacks.Add);
            events.Publish(new CharacterEquipmentChangedEvent(GrenadeItemId, OffhandType.None));
            events.Publish(new MoveInputEvent(Vector2.right));

            var target = new Vector2(5f, 1f);
            events.Publish(new PrimaryActionPhaseEvent(PrimaryActionPhase.Started, target));
            events.Publish(new PrimaryActionPressedEvent());
            events.Publish(new MoveInputEvent(Vector2.left));
            events.Publish(new PrimaryActionPhaseEvent(PrimaryActionPhase.Updated, target));

            Assert.That(character.ActionState, Is.EqualTo(CharacterActionState.ThrowAiming));
            Assert.That(character.MoveDirection, Is.EqualTo(Vector2.zero));
            Assert.That(attacks, Is.Empty, "按住瞄准期间不能生成攻击实例或消耗手雷。");

            events.Publish(new PrimaryActionPhaseEvent(PrimaryActionPhase.Released, target));
            events.Publish(new PrimaryActionPhaseEvent(PrimaryActionPhase.Released, target));

            Assert.That(character.ActionState, Is.EqualTo(CharacterActionState.Throwing));
            Assert.That(attacks.Count, Is.EqualTo(1));
            Assert.That(attacks[0].HasTargetWorldPosition, Is.True);
            Assert.That(attacks[0].TargetWorldPosition, Is.EqualTo(target));
            character.Dispose();
        }

        [Test]
        public void Throwable_CancelReturnsToNormalWithoutCreatingAttack()
        {
            var character = CreateCharacterSystem(out var events);
            var attackCount = 0;
            events.Subscribe<CharacterAttackRequestedEvent>(_ => attackCount++);
            events.Publish(new CharacterEquipmentChangedEvent(GrenadeItemId, OffhandType.None));
            events.Publish(new PrimaryActionPhaseEvent(PrimaryActionPhase.Started, new Vector2(4f, 0f)));
            events.Publish(new PrimaryActionPhaseEvent(PrimaryActionPhase.Canceled, Vector2.zero));

            Assert.That(character.ActionState, Is.EqualTo(CharacterActionState.Normal));
            Assert.That(attackCount, Is.Zero);
            character.Dispose();
        }

        [Test]
        public void Throwable_RightMouseUsesBlockInsteadOfLegacyAim()
        {
            var character = CreateCharacterSystem(out var events);
            events.Publish(new CharacterEquipmentChangedEvent(GrenadeItemId, OffhandType.None));
            events.Publish(new SecondaryActionInputEvent(true, Vector2.right));

            Assert.That(character.ActionState, Is.EqualTo(CharacterActionState.Blocking));
            Assert.That(character.IsBlocking, Is.True);
            character.Dispose();
        }

        [Test]
        public void Dodge_UpdatesStaminaImmediatelyForHudObservers()
        {
            var character = CreateCharacterSystem(out var events);
            var statsChangedCount = 0;
            events.Subscribe<CharacterStatsChangedEvent>(_ => statsChangedCount++);

            events.Publish(new DodgePressedEvent());

            Assert.That(character.Data.stamina, Is.EqualTo(24));
            Assert.That(statsChangedCount, Is.EqualTo(1));
            character.Dispose();
        }

        private static CharacterSystem CreateCharacterSystem(out EventBus events)
        {
            var database = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>("Assets/GameConfigs/GameConfigDatabase.asset");
            Assert.That(database, Is.Not.Null);

            events = new EventBus();
            var configs = new ConfigManager(database);
            var context = new GameContext(events, configs, null, null, null);
            configs.Initialize(context);
            var character = new CharacterSystem();
            character.Initialize(context);
            return character;
        }

        private static GameContext CreateContext(EventBus events)
        {
            var database = AssetDatabase.LoadAssetAtPath<GameConfigDatabase>("Assets/GameConfigs/GameConfigDatabase.asset");
            Assert.That(database, Is.Not.Null);
            var configs = new ConfigManager(database);
            var context = new GameContext(events, configs, null, null, null);
            configs.Initialize(context);
            return context;
        }
    }
}
