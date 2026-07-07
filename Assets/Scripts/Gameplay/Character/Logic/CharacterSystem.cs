using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Gameplay.Character.Data;
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

        public string ModuleKey => "character";
        public CharacterData Data => data;
        public Vector2 MoveDirection => moveDirection;
        public bool IsRunning => isRunning;
        public bool IsBlocking => isBlocking;

        public void Initialize(GameContext context)
        {
            this.context = context;
            context.Events.Subscribe<MoveInputEvent>(HandleMoveInput);
            context.Events.Subscribe<RunInputEvent>(HandleRunInput);
            context.Events.Subscribe<BlockInputEvent>(HandleBlockInput);
            context.Events.Subscribe<DodgePressedEvent>(HandleDodgePressed);
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<MoveInputEvent>(HandleMoveInput);
                context.Events.Unsubscribe<RunInputEvent>(HandleRunInput);
                context.Events.Unsubscribe<BlockInputEvent>(HandleBlockInput);
                context.Events.Unsubscribe<DodgePressedEvent>(HandleDodgePressed);
            }

            context = null;
            data = new CharacterData();
            moveDirection = Vector2.zero;
            isRunning = false;
            isBlocking = false;
        }

        public void Tick(float deltaTime)
        {
            if (data.isDead)
            {
                return;
            }

            if (isRunning && moveDirection.sqrMagnitude > 0f)
            {
                data.stamina = Mathf.Max(0, data.stamina - Mathf.CeilToInt(deltaTime * 10f));
                if (data.stamina == 0)
                {
                    isRunning = false;
                }
            }
            else
            {
                data.stamina = Mathf.Min(100, data.stamina + Mathf.CeilToInt(deltaTime * 8f));
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

        private void HandleDodgePressed(DodgePressedEvent evt)
        {
            if (data.stamina >= 20)
            {
                data.stamina -= 20;
                context.Events.Publish(new CharacterStatsChangedEvent());
            }
        }
    }
}
