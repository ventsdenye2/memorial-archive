using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Character.Logic;
using MemorialArchive.Gameplay.Character.Data;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Item.Config;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace MemorialArchive.Gameplay.Character.View
{
    /// <summary>
    /// Player Spine presentation.  This View consumes already-approved gameplay
    /// events and never owns damage, stamina, or combat-state decisions.
    /// </summary>
    public sealed class CharacterAnimationView : MonoBehaviour
    {
        private const int FireAxeItemId = 1003;

        private enum LocomotionState { Idle, Walk, Run, Dodge }
        private enum ActionPresentation { None, Equip, Attack, Block, Aim, Hurt, Reload, Death }

        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [Header("Locomotion")]
        [SerializeField] private SkeletonDataAsset idleData;
        [SerializeField] private SkeletonDataAsset walkData;
        [SerializeField] private SkeletonDataAsset runData;
        [SerializeField] private SkeletonDataAsset dodgeData;
        [SerializeField] private SkeletonDataAsset hurtLocomotionData;
        [Header("Fire axe")]
        [SerializeField] private SkeletonDataAsset fireAxeAttack1Data;
        [SerializeField] private SkeletonDataAsset fireAxeAttack2Data;
        [SerializeField] private SkeletonDataAsset fireAxeAttack3Data;
        [SerializeField] private SkeletonDataAsset fireAxeEquipData;
        [Header("Shared combat fallback")]
        [SerializeField] private SkeletonDataAsset bayonetEquipData;
        [SerializeField] private SkeletonDataAsset bayonetCombatData;
        [SerializeField] private SkeletonDataAsset shieldBlockData;
        [SerializeField] private string dodgeDisplacementBoneName = "bone";
        [SerializeField] private float characterScale = 1f;
        [SerializeField] private float stateMinDuration = 0.2f;

        private CharacterSystem character;
        private LocomotionState current = LocomotionState.Idle;
        private ActionPresentation activeAction;
        private bool facingRight = true;
        private bool isDead;
        private float stateEnteredAt;
        private int selectedItemId;
        private OffhandType equippedOffhand;
        private int sharedMeleeComboStage;
        private int fireAxeComboStage;
        private CharacterActionState presentedState = CharacterActionState.Normal;
        private TrackEntry dodgeTrackEntry;
        private TrackEntry actionTrackEntry;

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
            var events = GameRoot.Instance?.Context?.Events;
            events?.Subscribe<DodgeRequestedEvent>(HandleDodgeRequested);
            events?.Subscribe<SelectedItemChangedEvent>(HandleSelectedItemChanged);
            events?.Subscribe<CharacterActionStateChangedEvent>(HandleCharacterStateChanged);
            events?.Subscribe<CharacterEquipmentChangedEvent>(HandleCharacterEquipmentChanged);
            events?.Subscribe<BlockInputEvent>(HandleBlockInput);
            events?.Subscribe<AimInputEvent>(HandleAimInput);
            events?.Subscribe<CharacterDiedEvent>(HandleCharacterDied);

            if (skeletonAnimation == null)
            {
                enabled = false;
                return;
            }

            InitializeLocomotion();
        }

        private void OnDisable()
        {
            var events = GameRoot.Instance?.Context?.Events;
            events?.Unsubscribe<DodgeRequestedEvent>(HandleDodgeRequested);
            events?.Unsubscribe<SelectedItemChangedEvent>(HandleSelectedItemChanged);
            events?.Unsubscribe<CharacterActionStateChangedEvent>(HandleCharacterStateChanged);
            events?.Unsubscribe<CharacterEquipmentChangedEvent>(HandleCharacterEquipmentChanged);
            events?.Unsubscribe<BlockInputEvent>(HandleBlockInput);
            events?.Unsubscribe<AimInputEvent>(HandleAimInput);
            events?.Unsubscribe<CharacterDiedEvent>(HandleCharacterDied);
            if (current == LocomotionState.Dodge) PublishDodgeAnimationState(false);
            UnsubscribeDodgeComplete();
            UnsubscribeActionComplete();
        }

        private void Update()
        {
            if (character == null)
            {
                character = GameRoot.Instance?.GetSystem<CharacterSystem>();
                if (character == null) return;
            }

            if (isDead || current == LocomotionState.Dodge || activeAction != ActionPresentation.None) return;
            SampleLocomotion();
        }

        private void SampleLocomotion()
        {
            var moving = character.MoveDirection.sqrMagnitude > 0.0001f;
            var desired = !moving ? LocomotionState.Idle : character.IsRunning ? LocomotionState.Run : LocomotionState.Walk;
            if (desired != current && CanTransitionTo(desired))
            {
                var isInjured = character.Data != null && character.Data.health <= 1;
                switch (desired)
                {
                    case LocomotionState.Idle:
                        SwitchSkeleton(idleData, "idle", true);
                        break;
                    case LocomotionState.Walk:
                        SwitchSkeleton(isInjured ? hurtLocomotionData : walkData, isInjured ? "hurt-walk" : "animation", true);
                        break;
                    case LocomotionState.Run:
                        SwitchSkeleton(isInjured ? hurtLocomotionData : runData, isInjured ? "hurt-run" : "animation", true);
                        break;
                }
                current = desired;
                stateEnteredAt = Time.time;
            }
            UpdateFacing();
        }

        private bool CanTransitionTo(LocomotionState desired)
        {
            return desired == LocomotionState.Idle || current == LocomotionState.Idle || Time.time - stateEnteredAt >= stateMinDuration;
        }

        private void InitializeLocomotion()
        {
            isDead = false;
            activeAction = ActionPresentation.None;
            current = LocomotionState.Idle;
            stateEnteredAt = Time.time;
            SwitchSkeleton(idleData, "idle", true);
        }

        private void HandleSelectedItemChanged(SelectedItemChangedEvent evt)
        {
            selectedItemId = evt.Item?.itemId ?? 0;
            sharedMeleeComboStage = 0;
            fireAxeComboStage = 0;
            if (isDead || current == LocomotionState.Dodge) return;

            if (activeAction == ActionPresentation.Block || activeAction == ActionPresentation.Aim) StopActionToLocomotion();
            if (SelectedItemUses(CombatAttackKind.Melee))
            {
                StartOneShot(selectedItemId == FireAxeItemId ? fireAxeEquipData : bayonetEquipData, "chixie", ActionPresentation.Equip);
            }
        }

        private void HandleCharacterEquipmentChanged(CharacterEquipmentChangedEvent evt)
        {
            selectedItemId = evt.PrimaryItemId;
            equippedOffhand = evt.OffhandType;
        }

        private void HandleCharacterStateChanged(CharacterActionStateChangedEvent evt)
        {
            presentedState = evt.State;
            if (evt.State == CharacterActionState.Dead) { HandleCharacterDied(new CharacterDiedEvent(0f)); return; }
            if (evt.State == CharacterActionState.Staggered) { StartOneShot(bayonetCombatData, "hurt1", ActionPresentation.Hurt); return; }
            if (evt.State == CharacterActionState.Attack1 || evt.State == CharacterActionState.Attack2 || evt.State == CharacterActionState.Attack3)
            {
                PlayStateAttack(evt.State);
            }
        }

        private void PlayStateAttack(CharacterActionState state)
        {
            var config = GameRoot.Instance?.Context?.Configs.GetItem(selectedItemId);
            if (config != null && config.CombatAttackKind != CombatAttackKind.Melee) { StartOneShot(bayonetCombatData, "throw", ActionPresentation.Attack); return; }
            var stage = state == CharacterActionState.Attack1 ? 1 : state == CharacterActionState.Attack2 ? 2 : 3;
            if (selectedItemId == FireAxeItemId) StartOneShot(stage == 1 ? fireAxeAttack1Data : stage == 2 ? fireAxeAttack2Data : fireAxeAttack3Data, stage == 1 ? "act1_both hands" : stage == 2 ? "act2 both hands" : "act3 both hands", ActionPresentation.Attack);
            else StartOneShot(bayonetCombatData, stage == 1 ? "act1（single）" : stage == 2 ? "act2（single）" : "act3（single）", ActionPresentation.Attack);
        }

        private void PlayMeleeAttack(int weaponItemId)
        {
            if (weaponItemId == FireAxeItemId)
            {
                fireAxeComboStage = fireAxeComboStage % 3 + 1;
                StartOneShot(
                    fireAxeComboStage == 1 ? fireAxeAttack1Data : fireAxeComboStage == 2 ? fireAxeAttack2Data : fireAxeAttack3Data,
                    fireAxeComboStage == 1 ? "act1_both hands" : fireAxeComboStage == 2 ? "act2 both hands" : "act3 both hands",
                    ActionPresentation.Attack);
                return;
            }

            // 匕首、刺刀、军官佩剑等近战武器暂共用刺刀三段动作。
            sharedMeleeComboStage = sharedMeleeComboStage % 3 + 1;
            StartOneShot(
                bayonetCombatData,
                sharedMeleeComboStage == 1 ? "act1（single）" : sharedMeleeComboStage == 2 ? "act2（single）" : "act3（single）",
                ActionPresentation.Attack);
        }

        private void HandleBlockInput(BlockInputEvent evt)
        {
            if (isDead || current == LocomotionState.Dodge) return;
            if (!evt.IsBlocking)
            {
                if (activeAction == ActionPresentation.Block) StopActionToLocomotion();
                return;
            }

            if (activeAction != ActionPresentation.None && activeAction != ActionPresentation.Block) return;
            if (equippedOffhand == OffhandType.Shield)
            {
                StartLoop(shieldBlockData, "Block_Shield", ActionPresentation.Block);
            }
            else
            {
                // 空手及非盾牌格挡动作资源尚未分别补齐，统一使用刺刀格挡作为表现回退。
                StartLoop(bayonetCombatData, "Block", ActionPresentation.Block);
            }
        }

        private void HandleAimInput(AimInputEvent evt)
        {
            if (isDead || current == LocomotionState.Dodge) return;
            if (!evt.IsAiming)
            {
                if (activeAction == ActionPresentation.Aim) StopActionToLocomotion();
                return;
            }

            if (activeAction != ActionPresentation.None && activeAction != ActionPresentation.Aim) return;
            if (SelectedItemUses(CombatAttackKind.Throwable) || SelectedItemUses(CombatAttackKind.Firearm))
            {
                // 枪械瞄准资源缺失，暂复用投掷瞄准姿势。
                StartLoop(bayonetCombatData, "throw_aim", ActionPresentation.Aim);
            }
        }

        private void HandleCharacterDied(CharacterDiedEvent evt)
        {
            isDead = true;
            CancelCurrentAction();
            UnsubscribeDodgeComplete();
            current = LocomotionState.Idle;
            SwitchSkeleton(bayonetCombatData, "death", false);
        }

        private void HandleDodgeRequested(DodgeRequestedEvent evt)
        {
            if (isDead) return;
            CancelCurrentAction();
            current = LocomotionState.Dodge;
            stateEnteredAt = Time.time;
            UnsubscribeDodgeComplete();
            var entry = SwitchSkeleton(dodgeData, "dodge", false);
            if (entry == null)
            {
                SwitchToLocomotion();
                return;
            }

            PublishDodgeAnimationState(true);
            dodgeTrackEntry = entry;
            dodgeTrackEntry.Complete += HandleDodgeComplete;
        }

        private void HandleDodgeComplete(TrackEntry trackEntry)
        {
            if (trackEntry != dodgeTrackEntry) return;
            CommitDodgePosition();
            SwitchToLocomotion();
        }

        private void StartOneShot(SkeletonDataAsset data, string animationName, ActionPresentation presentation)
        {
            if (data == null || isDead || current == LocomotionState.Dodge) return;
            CancelCurrentAction();
            activeAction = presentation;
            var entry = SwitchSkeleton(data, animationName, false);
            if (entry == null)
            {
                activeAction = ActionPresentation.None;
                SwitchToLocomotion();
                return;
            }

            actionTrackEntry = entry;
            actionTrackEntry.Complete += HandleActionComplete;
        }

        private void StartLoop(SkeletonDataAsset data, string animationName, ActionPresentation presentation)
        {
            if (data == null || isDead || current == LocomotionState.Dodge) return;
            if (activeAction == presentation && skeletonAnimation.skeletonDataAsset == data) return;
            CancelCurrentAction();
            activeAction = presentation;
            SwitchSkeleton(data, animationName, true);
        }

        private void HandleActionComplete(TrackEntry trackEntry)
        {
            if (trackEntry != actionTrackEntry) return;
            GameRoot.Instance?.Context?.Events.Publish(new CharacterActionAnimationCompletedEvent(presentedState));
            UnsubscribeActionComplete();
            activeAction = ActionPresentation.None;
            SwitchToLocomotion();
        }

        private void StopActionToLocomotion()
        {
            CancelCurrentAction();
            SwitchToLocomotion();
        }

        private void CancelCurrentAction()
        {
            UnsubscribeActionComplete();
            activeAction = ActionPresentation.None;
        }

        private bool SelectedItemUses(CombatAttackKind attackKind)
        {
            var config = GameRoot.Instance?.Context?.Configs.GetItem(selectedItemId);
            return config != null && config.CombatAttackKind == attackKind;
        }

        private void SwitchToLocomotion()
        {
            if (isDead) return;
            if (current == LocomotionState.Dodge) PublishDodgeAnimationState(false);
            UnsubscribeDodgeComplete();
            current = LocomotionState.Idle;
            stateEnteredAt = Time.time;
            SwitchSkeleton(idleData, "idle", true);
        }

        private void CommitDodgePosition()
        {
            var skeleton = skeletonAnimation != null ? skeletonAnimation.skeleton : null;
            var displacementBone = skeleton?.FindBone(dodgeDisplacementBoneName);
            if (displacementBone == null)
            {
                Debug.LogWarning($"Dodge displacement bone '{dodgeDisplacementBoneName}' was not found.", this);
                return;
            }

            var localOffsetX = displacementBone.X - displacementBone.Data.X;
            var facingScale = skeleton != null ? skeleton.ScaleX : 1f;
            var worldDelta = skeletonAnimation.transform.TransformVector(new Vector3(localOffsetX * facingScale, 0f, 0f));
            GameRoot.Instance?.Context?.Events.Publish(new DodgePositionDeltaEvent(new Vector2(worldDelta.x, 0f)));
        }

        private static void PublishDodgeAnimationState(bool isPlaying)
        {
            GameRoot.Instance?.Context?.Events.Publish(new DodgeAnimationStateChangedEvent(isPlaying));
        }

        private TrackEntry SwitchSkeleton(SkeletonDataAsset data, string animationName, bool loop)
        {
            if (skeletonAnimation == null || data == null) return null;
            if (skeletonAnimation.skeletonDataAsset != data || skeletonAnimation.state == null)
            {
                // Spine's renderer performs a LateUpdate while rebuilding the skeleton.
                // Clear tracks first so timelines from the previous skeleton are not
                // evaluated against the new skeleton's bone/slot layout.
                skeletonAnimation.ClearState();
                skeletonAnimation.skeletonDataAsset = data;
                skeletonAnimation.Initialize(true);
            }
            var entry = skeletonAnimation.state.SetAnimation(0, animationName, loop);
            ApplyFacing();
            return entry;
        }

        private void UpdateFacing()
        {
            if (character.MoveDirection.sqrMagnitude > 0.0001f) UpdateFacing(character.MoveDirection);
        }

        private void UpdateFacing(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) <= 0.0001f) return;
            facingRight = direction.x >= 0f;
            ApplyFacing();
        }

        private void ApplyFacing()
        {
            if (skeletonAnimation != null && skeletonAnimation.skeleton != null)
            {
                skeletonAnimation.skeleton.ScaleX = facingRight ? 1f : -1f;
            }
        }

        private void UnsubscribeDodgeComplete()
        {
            if (dodgeTrackEntry == null) return;
            dodgeTrackEntry.Complete -= HandleDodgeComplete;
            dodgeTrackEntry = null;
        }

        private void UnsubscribeActionComplete()
        {
            if (actionTrackEntry == null) return;
            actionTrackEntry.Complete -= HandleActionComplete;
            actionTrackEntry = null;
        }
    }
}
