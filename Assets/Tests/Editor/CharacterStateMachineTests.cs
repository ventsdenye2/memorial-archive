using MemorialArchive.Framework.Config;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Character.Data;
using MemorialArchive.Gameplay.Character.Logic;
using MemorialArchive.Gameplay.Item.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MemorialArchive.Tests.Editor
{
    public sealed class CharacterStateMachineTests
    {
        private const int FireAxeItemId = 1003;

        [Test]
        public void PrimaryAction_DoesNotRestartUntilCurrentAttackAnimationCompletes()
        {
            var character = CreateCharacterSystem(out var events);
            var attackRequestCount = 0;
            events.Subscribe<CharacterAttackRequestedEvent>(_ => attackRequestCount++);
            events.Publish(new CharacterEquipmentChangedEvent(FireAxeItemId, OffhandType.None));

            events.Publish(new PrimaryActionPressedEvent());
            var activeAttackState = character.ActionState;
            events.Publish(new PrimaryActionPressedEvent());

            Assert.That(activeAttackState, Is.EqualTo(CharacterActionState.Attack1));
            Assert.That(character.ActionState, Is.EqualTo(activeAttackState));
            Assert.That(attackRequestCount, Is.EqualTo(1));

            events.Publish(new CharacterActionAnimationCompletedEvent(activeAttackState));
            events.Publish(new PrimaryActionPressedEvent());
            Assert.That(attackRequestCount, Is.EqualTo(2));
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
        public void Attacking_ClearsMovementAndRejectsFurtherMoveInput()
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
    }
}
