using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Gameplay.Character.Data;
using MemorialArchive.Gameplay.Character.Config;
using MemorialArchive.Gameplay.Stage1;
using UnityEngine;

namespace MemorialArchive.Gameplay.Character.Logic
{
    public sealed class CharacterSystem : IGameSystem, ITickableSystem, ISaveModule
    {
        private CharacterData data = new CharacterData();
        private GameContext context;
        private Vector2 moveDirection;
        private bool isRunning;
        private bool isBlocking;
        private bool isAiming;
        private Vector2 aimDirection = Vector2.right;
        private CharacterAttributeConfig attributes;
        private float exactStamina;
        private float dodgeCooldownRemaining;

        public string ModuleKey => "character";
        public CharacterData Data => data;
        public Vector2 MoveDirection => moveDirection;
        public bool IsRunning => isRunning;
        public bool IsBlocking => isBlocking;
        public bool IsAiming => isAiming;
        public Vector2 AimDirection => aimDirection;
        public float CurrentMoveSpeed => attributes == null ? 0f : (isRunning ? attributes.RunSpeed : attributes.WalkSpeed);

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<MoveInputEvent>(HandleMoveInput);
            context.Events.Subscribe<RunInputEvent>(HandleRunInput);
            context.Events.Subscribe<BlockInputEvent>(HandleBlockInput);
            context.Events.Subscribe<AimInputEvent>(HandleAimInput);
            context.Events.Subscribe<DodgePressedEvent>(HandleDodgePressed);

            data.attributeId = Stage1Ids.PlayerAttributeId;
            data.stateId = Stage1Ids.PlayerNormalStateId;
            attributes = context.Configs.GetCharacterAttribute(data.attributeId);
            if (attributes == null)
            {
                Debug.LogError($"Missing player CharacterAttributeConfig id={data.attributeId}.");
                data.health = 3;
                data.stamina = 30;
            }
            else
            {
                data.health = attributes.MaxHealth;
                data.stamina = attributes.MaxStamina;
            }

            exactStamina = data.stamina;
            context.Events.Publish(new CharacterStatsChangedEvent());
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<MoveInputEvent>(HandleMoveInput);
                context.Events.Unsubscribe<RunInputEvent>(HandleRunInput);
                context.Events.Unsubscribe<BlockInputEvent>(HandleBlockInput);
                context.Events.Unsubscribe<AimInputEvent>(HandleAimInput);
                context.Events.Unsubscribe<DodgePressedEvent>(HandleDodgePressed);
            }

            context = null;
            data = new CharacterData();
            moveDirection = Vector2.zero;
            isRunning = false;
            isBlocking = false;
            isAiming = false;
            attributes = null;
        }

        public void Tick(float deltaTime)
        {
            if (data.isDead)
            {
                return;
            }

            if (dodgeCooldownRemaining > 0f)
            {
                dodgeCooldownRemaining = Mathf.Max(0f, dodgeCooldownRemaining - deltaTime);
            }

            if (attributes == null)
            {
                return;
            }

            var before = data.stamina;
            if (isRunning && moveDirection.sqrMagnitude > 0f)
            {
                exactStamina = Mathf.Max(0f, exactStamina - attributes.RunStaminaCostPerSecond * deltaTime);
                if (exactStamina <= 0f)
                {
                    isRunning = false;
                }
            }
            else
            {
                exactStamina = Mathf.Min(attributes.MaxStamina, exactStamina + attributes.StaminaRecoveryPerSecond * deltaTime);
            }

            data.stamina = Mathf.Clamp(Mathf.CeilToInt(exactStamina), 0, attributes.MaxStamina);
            if (data.stamina != before)
            {
                context.Events.Publish(new CharacterStatsChangedEvent());
            }
        }

        public void ApplyDamage(int damage)
        {
            if (data.isDead)
            {
                return;
            }

            data.health = Mathf.Max(0, data.health - Mathf.Max(0, damage));
            context.Events.Publish(new CharacterStatsChangedEvent());
            if (data.health == 0)
            {
                data.isDead = true;
                context.Events.Publish(new CharacterDiedEvent(1.2f));
            }
        }

        public object CaptureSaveData()
        {
            return data;
        }

        public void RestoreSaveData(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var restored = JsonUtility.FromJson<CharacterData>(json);
            if (restored != null)
            {
                data = restored;
                attributes = context.Configs.GetCharacterAttribute(data.attributeId);
                exactStamina = data.stamina;
                context.Events.Publish(new CharacterStatsChangedEvent());
            }
        }

        private void HandleMoveInput(MoveInputEvent evt)
        {
            moveDirection = Vector2.ClampMagnitude(evt.Direction, 1f);
        }

        private void HandleRunInput(RunInputEvent evt)
        {
            isRunning = evt.IsRunning && data.stamina > 0;
        }

        private void HandleBlockInput(BlockInputEvent evt)
        {
            isBlocking = evt.IsBlocking;
        }

        private void HandleAimInput(AimInputEvent evt)
        {
            isAiming = evt.IsAiming;
            if (!isAiming)
            {
                return;
            }

            var direction = evt.PointerWorldPosition - data.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                aimDirection = direction.normalized;
            }
        }

        private void HandleDodgePressed(DodgePressedEvent evt)
        {
            if (attributes == null || dodgeCooldownRemaining > 0f || exactStamina < attributes.DodgeStaminaCost)
            {
                return;
            }


            var direction = moveDirection.sqrMagnitude > 0.0001f ? moveDirection.normalized : aimDirection;
            exactStamina -= attributes.DodgeStaminaCost;
            data.stamina = Mathf.CeilToInt(exactStamina);
            dodgeCooldownRemaining = attributes.DodgeCooldownSeconds;
            context.Events.Publish(new CharacterStatsChangedEvent());
            context.Events.Publish(new DodgeRequestedEvent(direction, attributes.DodgeDistance, attributes.DodgeDurationSeconds));
        }
    }
}
