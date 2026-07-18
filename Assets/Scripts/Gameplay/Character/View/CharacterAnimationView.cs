using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Character.Logic;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace MemorialArchive.Gameplay.Character.View
{
    // 角色动画 View：监听 CharacterSystem 的运动状态与 DodgeRequestedEvent，
    // 在 Idle/Walk/Run/Dodge 四套 SkeletonDataAsset 之间切换。
    // 纯 View：只读 CharacterSystem，不写其状态。生命周期参考 PlayerMotor / MonsterSpawnPointView。
    public sealed class CharacterAnimationView : MonoBehaviour
    {
        private enum LocomotionState
        {
            Idle,
            Walk,
            Run,
            Dodge
        }

        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [SerializeField] private SkeletonDataAsset idleData;
        [SerializeField] private SkeletonDataAsset walkData;
        [SerializeField] private SkeletonDataAsset runData;
        [SerializeField] private SkeletonDataAsset dodgeData;

        // Spine 角色默认 0.01 scale 下高度约 3.56 world unit；
        // 这里让 PlayerVisual 本地缩放。1.0 → 角色最终约 2.5 unit 高（Player 根 scale 0.7）。
        [SerializeField] private float characterScale = 1f;

        // 进入新状态后的最小持续时间，避免 CharacterSystem 体力边界 isRunning toggle
        // 导致动画高频切换（CharacterSystem.Tick 把 isRunning 设 false，下一帧 InputReader
        // 又发 RunInputEvent(true)，stamina CeilToInt 在 0/1 之间反复）。
        [SerializeField] private float stateMinDuration = 0.2f;

        private CharacterSystem character;
        private LocomotionState current = LocomotionState.Idle;
        private bool facingRight = true;
        private float dodgeRemaining;
        private float stateEnteredAt;
        private bool subscribedToDodgeComplete;

        private void Awake()
        {
            if (skeletonAnimation != null && skeletonAnimation.transform.parent == transform)
            {
                skeletonAnimation.transform.localScale = new Vector3(characterScale, characterScale, 1f);
            }
        }

        private void OnEnable()
        {
            character = GameRoot.Instance?.GetSystem<CharacterSystem>();
            GameRoot.Instance?.Context?.Events.Subscribe<DodgeRequestedEvent>(HandleDodgeRequested);

            if (skeletonAnimation == null)
            {
                enabled = false;
                return;
            }

            InitializeLocomotion();
        }

        private void OnDisable()
        {
            GameRoot.Instance?.Context?.Events.Unsubscribe<DodgeRequestedEvent>(HandleDodgeRequested);
            UnsubscribeDodgeComplete();
        }

        private void Update()
        {
            if (character == null)
            {
                character = GameRoot.Instance?.GetSystem<CharacterSystem>();
                if (character == null)
                {
                    return;
                }
            }

            if (dodgeRemaining > 0f)
            {
                dodgeRemaining = Mathf.Max(0f, dodgeRemaining - Time.deltaTime);
                if (dodgeRemaining == 0f)
                {
                    SwitchToLocomotion();
                }
                return;
            }

            SampleLocomotion();
        }

        private void SampleLocomotion()
        {
            var moving = character.MoveDirection.sqrMagnitude > 0.0001f;
            var desired = !moving ? LocomotionState.Idle
                          : character.IsRunning ? LocomotionState.Run
                          : LocomotionState.Walk;

            if (desired != current && CanTransitionTo(desired))
            {
                switch (desired)
                {
                    case LocomotionState.Idle: SwitchSkeleton(idleData, "idle", true); break;
                    case LocomotionState.Walk: SwitchSkeleton(walkData, "animation", true); break;
                    case LocomotionState.Run:  SwitchSkeleton(runData, "animation", true); break;
                }
                current = desired;
                stateEnteredAt = Time.time;
            }

            UpdateFacing();
        }

        // 滞回：当前状态持续不够久就不切，避开 CharacterSystem 在 stamina 边界的 isRunning toggle。
        // Idle<->Walk 的转换不受限制（玩家停止/启动移动应立即响应）。
        private bool CanTransitionTo(LocomotionState desired)
        {
            if (desired == LocomotionState.Idle || current == LocomotionState.Idle)
            {
                return true;
            }
            return Time.time - stateEnteredAt >= stateMinDuration;
        }

        private void InitializeLocomotion()
        {
            current = LocomotionState.Idle;
            stateEnteredAt = Time.time;
            SwitchSkeleton(idleData, "idle", true);
        }

        private void SwitchToLocomotion()
        {
            UnsubscribeDodgeComplete();
            // 重置为 Idle，下一帧 SampleLocomotion 会按 MoveDirection 校正到 Walk/Run。
            current = LocomotionState.Idle;
            stateEnteredAt = Time.time;
            SwitchSkeleton(idleData, "idle", true);
        }

        private void HandleDodgeRequested(DodgeRequestedEvent evt)
        {
            dodgeRemaining = Mathf.Max(0.05f, evt.DurationSeconds);
            facingRight = evt.Direction.x >= 0f;
            current = LocomotionState.Dodge;
            stateEnteredAt = Time.time;
            SwitchSkeleton(dodgeData, "dodge", false);
            SubscribeDodgeComplete();
        }

        // 双保险：dodge 动画（0.933s）自然结束时也切回 locomotion，
        // 防止 dodgeRemaining 计时和动画时长有偏差导致动画卡在最后一帧。
        private void HandleDodgeComplete(TrackEntry trackEntry)
        {
            dodgeRemaining = 0f;
            SwitchToLocomotion();
        }

        private void SwitchSkeleton(SkeletonDataAsset data, string animName, bool loop)
        {
            if (skeletonAnimation == null || data == null)
            {
                return;
            }

            // 首次进入或 SkeletonAnimation 还没 Awake 完时（典型场景：Stage1DemoRoot 从 inactive 切 active，
            // OnEnable 跑得比 SkeletonAnimation.Awake 早），state 会是 null。这里强制 Initialize 兜底。
            if (skeletonAnimation.skeletonDataAsset != data || skeletonAnimation.state == null)
            {
                skeletonAnimation.skeletonDataAsset = data;
                skeletonAnimation.Initialize(true); // 重建 skeleton/state/mesh；内部会自动 LateUpdate
            }

            skeletonAnimation.state.SetAnimation(0, animName, loop);
            ApplyFacing();
        }

        private void UpdateFacing()
        {
            if (character.MoveDirection.sqrMagnitude > 0.0001f)
            {
                var right = character.MoveDirection.x >= 0f;
                if (right != facingRight)
                {
                    facingRight = right;
                    ApplyFacing();
                }
            }
        }

        private void ApplyFacing()
        {
            if (skeletonAnimation != null && skeletonAnimation.skeleton != null)
            {
                skeletonAnimation.skeleton.ScaleX = facingRight ? 1f : -1f;
            }
        }

        private void SubscribeDodgeComplete()
        {
            if (subscribedToDodgeComplete || skeletonAnimation == null || skeletonAnimation.state == null)
            {
                return;
            }
            skeletonAnimation.state.Complete += HandleDodgeComplete;
            subscribedToDodgeComplete = true;
        }

        private void UnsubscribeDodgeComplete()
        {
            if (!subscribedToDodgeComplete || skeletonAnimation == null || skeletonAnimation.state == null)
            {
                subscribedToDodgeComplete = false;
                return;
            }
            skeletonAnimation.state.Complete -= HandleDodgeComplete;
            subscribedToDodgeComplete = false;
        }
    }
}
