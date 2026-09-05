using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Character.Logic;
using MemorialArchive.Gameplay.Character.Data;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Combat.View;
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
        private const int BayonetItemId = 1002;
        private const float FirearmShotLifetimeSafetySeconds = 0.25f;

        private enum LocomotionState { Idle, Walk, Run, Dodge }
        private enum ActionPresentation { None, Equip, Attack, Block, Aim, Hurt, Reload, Death }

        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [Header("Locomotion")]
        [SerializeField] private SkeletonDataAsset idleData;
        [SerializeField] private SkeletonDataAsset walkData;
        [SerializeField] private SkeletonDataAsset runData;
        [SerializeField] private SkeletonDataAsset dodgeData;
        [SerializeField] private SkeletonDataAsset lanternDodgeData;
        [SerializeField] private SkeletonDataAsset hurtLocomotionData;
        [Header("Fire axe")]
        [SerializeField] private SkeletonDataAsset fireAxeAttack1Data;
        [SerializeField] private SkeletonDataAsset fireAxeAttack2Data;
        [SerializeField] private SkeletonDataAsset fireAxeAttack3Data;
        [Header("One-handed weapon")]
        [SerializeField] private SkeletonDataAsset oneHandedAttackData;
        [SerializeField] private SkeletonDataAsset weaponEquipData;
        [Header("Updated weapon actions")]
        [SerializeField] private SkeletonDataAsset updatedBayonetAttackData;
        [SerializeField] private SkeletonDataAsset updatedGrenadeThrowData;
        [Header("Shared combat fallback")]
        [SerializeField] private SkeletonDataAsset bayonetCombatData;
        [SerializeField] private SkeletonDataAsset shieldBlockData;
        [Header("Firearm")]
        [SerializeField] private SkeletonDataAsset firearmActionData;
        [SerializeField] private SkeletonDataAsset armedHurtData;
        [SerializeField] private string firearmAimBoneName = "rotate-weapon";
        [SerializeField] private float firearmAimAngleOffset;
        [SerializeField, Min(0f)] private float gunShotStartTimeSeconds = 1.7f;
        [SerializeField, Min(0f)] private float firearmShotDelayAfterReleaseSeconds = 0.35f;
        [SerializeField, Min(0f)] private float dodgeArcHeight = 0.16f;
        [SerializeField] private float characterScale = 1f;
        [SerializeField] private float stateMinDuration = 0.2f;
        [SerializeField, Min(0f)] private float throwableAimHoldTime = 0.1f;

        private CharacterSystem character;
        private LocomotionState current = LocomotionState.Idle;
        private ActionPresentation activeAction;
        private bool facingRight = true;
        private bool isDead;
        private float stateEnteredAt;
        private int selectedItemId;
        private OffhandType equippedOffhand;
        private Vector2 firearmAimWorldPosition;
        private bool hasFirearmAimWorldPosition;
        private AttackContext pendingFirearmAttack;
        private TrackEntry pendingFirearmShotTrackEntry;
        private float pendingFirearmShotTargetTrackTime;
        private bool firearmShotFramePublished;
        private bool firearmShotLifetimeRequested;
        private int sharedMeleeComboStage;
        private int fireAxeComboStage;
        private CharacterActionState presentedState = CharacterActionState.Normal;
        private TrackEntry dodgeTrackEntry;
        private TrackEntry actionTrackEntry;
        private bool isUsingWalkingEquipAnimation;
        private float equipPlaybackDuration;
        private Vector3 dodgeVisualBasePosition;
        private bool hasCachedDodgeVisualBasePosition;

        private void Awake()
        {
            if (skeletonAnimation != null && skeletonAnimation.transform.parent == transform)
            {
                skeletonAnimation.transform.localScale = new Vector3(characterScale, characterScale, 1f);
            }

            CacheDodgeVisualBasePosition();
        }

        private void OnEnable()
        {
            character = GameRoot.Instance?.GetSystem<CharacterSystem>();
            var events = GameRoot.Instance?.Context?.Events;
            events?.Subscribe<DodgeRequestedEvent>(HandleDodgeRequested);
            events?.Subscribe<DodgeMotionProgressEvent>(HandleDodgeMotionProgress);
            events?.Subscribe<SelectedItemChangedEvent>(HandleSelectedItemChanged);
            events?.Subscribe<CharacterActionStateChangedEvent>(HandleCharacterStateChanged);
            events?.Subscribe<AttackStartedEvent>(HandleAttackStarted);
            events?.Subscribe<CharacterEquipmentChangedEvent>(HandleCharacterEquipmentChanged);
            events?.Subscribe<BlockInputEvent>(HandleBlockInput);
            events?.Subscribe<AimInputEvent>(HandleAimInput);
            events?.Subscribe<ReloadRequestedEvent>(HandleReloadRequested);
            events?.Subscribe<CharacterDiedEvent>(HandleCharacterDied);
            events?.Subscribe<DamageAppliedEvent>(HandleCombatDamage);
            events?.Subscribe<CharacterDamageReceivedEvent>(HandleCharacterDamageReceived);
            events?.Subscribe<CharacterItemEffectRequestedEvent>(HandleItemEffectRequested);

            if (skeletonAnimation == null)
            {
                enabled = false;
                return;
            }

            InitializeLocomotion();
        }

        private void OnDisable()
        {
            CancelCurrentAction();
            var events = GameRoot.Instance?.Context?.Events;
            events?.Unsubscribe<DodgeRequestedEvent>(HandleDodgeRequested);
            events?.Unsubscribe<DodgeMotionProgressEvent>(HandleDodgeMotionProgress);
            events?.Unsubscribe<SelectedItemChangedEvent>(HandleSelectedItemChanged);
            events?.Unsubscribe<CharacterActionStateChangedEvent>(HandleCharacterStateChanged);
            events?.Unsubscribe<AttackStartedEvent>(HandleAttackStarted);
            events?.Unsubscribe<CharacterEquipmentChangedEvent>(HandleCharacterEquipmentChanged);
            events?.Unsubscribe<BlockInputEvent>(HandleBlockInput);
            events?.Unsubscribe<AimInputEvent>(HandleAimInput);
            events?.Unsubscribe<ReloadRequestedEvent>(HandleReloadRequested);
            events?.Unsubscribe<CharacterDiedEvent>(HandleCharacterDied);
            events?.Unsubscribe<DamageAppliedEvent>(HandleCombatDamage);
            events?.Unsubscribe<CharacterDamageReceivedEvent>(HandleCharacterDamageReceived);
            events?.Unsubscribe<CharacterItemEffectRequestedEvent>(HandleItemEffectRequested);
            if (current == LocomotionState.Dodge) PublishDodgeAnimationState(false);
            UnsubscribeDodgeComplete();
            UnsubscribeActionComplete();
            ResetDodgeVisualOffset();
        }

        private void Update()
        {
            if (character == null)
            {
                character = GameRoot.Instance?.GetSystem<CharacterSystem>();
                if (character == null) return;
            }

            if (activeAction == ActionPresentation.Aim && character.IsAiming) UpdateFacing(character.AimDirection);
            if (activeAction == ActionPresentation.Equip)
            {
                SwitchEquipAnimation(character.MoveDirection.sqrMagnitude > 0.0001f);
            }
            if (isDead || current == LocomotionState.Dodge || activeAction != ActionPresentation.None) return;
            SampleLocomotion();
        }

        private void LateUpdate()
        {
            if (activeAction == ActionPresentation.Aim && SelectedItemUses(CombatAttackKind.Firearm) &&
                hasFirearmAimWorldPosition)
            {
                UpdateFirearmAim(firearmAimWorldPosition);
            }

            TryPublishFirearmShotFrame();
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
                        SwitchIdleAnimation();
                        break;
                    case LocomotionState.Walk:
                        SwitchSkeleton(isInjured ? hurtLocomotionData : walkData, isInjured ? "hurt-walk" : equippedOffhand == OffhandType.Lantern ? "lanternwalk" : "normal walk", true);
                        break;
                    case LocomotionState.Run:
                        SwitchSkeleton(isInjured ? hurtLocomotionData : runData, isInjured ? "hurt-run" : equippedOffhand == OffhandType.Lantern ? "run_lantern" : "normal run", true);
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
            SwitchIdleAnimation();
        }

        private void HandleSelectedItemChanged(SelectedItemChangedEvent evt)
        {
            ClearPendingFirearmShot();
            selectedItemId = evt.Item?.itemId ?? 0;
            hasFirearmAimWorldPosition = false;
            sharedMeleeComboStage = 0;
            fireAxeComboStage = 0;
            if (isDead || current == LocomotionState.Dodge) return;

            if (activeAction == ActionPresentation.Block || activeAction == ActionPresentation.Aim || activeAction == ActionPresentation.Equip) StopActionToLocomotion();
            if (SelectedItemUses(CombatAttackKind.Melee))
            {
                var equipWhileMoving = character != null && character.MoveDirection.sqrMagnitude > 0.0001f;
                if (StartEquipAnimation(equipWhileMoving))
                {
                    PublishEquipAnimationState(true);
                }
                return;
            }

            if (activeAction == ActionPresentation.None) SwitchToLocomotion();
        }

        private void HandleCharacterEquipmentChanged(CharacterEquipmentChangedEvent evt)
        {
            ClearPendingFirearmShot();
            selectedItemId = evt.PrimaryItemId;
            equippedOffhand = evt.OffhandType;
            hasFirearmAimWorldPosition = false;
            if (!isDead && current != LocomotionState.Dodge && activeAction == ActionPresentation.None)
            {
                SwitchToLocomotion();
            }
        }

        private void HandleCharacterStateChanged(CharacterActionStateChangedEvent evt)
        {
            presentedState = evt.State;
            // Throwing 会立即用 throw 动画替换瞄准姿势，无需先切回待机造成双重建骨。
            if (activeAction == ActionPresentation.Aim && evt.State != CharacterActionState.Aiming && evt.State != CharacterActionState.ThrowAiming && evt.State != CharacterActionState.Throwing)
                StopActionToLocomotion();
            if (evt.State == CharacterActionState.Dead) { HandleCharacterDied(new CharacterDiedEvent(0f)); return; }
            // Hurt animation is started from DamageAppliedEvent only.  A state
            // event can be replayed during initialization or by another view;
            // treating Staggered alone as damage used to cause self-triggered
            // hurt animations.
            if (evt.State == CharacterActionState.ThrowAiming)
            {
                StartHeldOneShot(GetThrowableActionData(), GetThrowableAimAnimationName(), ActionPresentation.Aim);
                if (character != null) UpdateFacing(character.AimDirection);
                return;
            }
            if (evt.State == CharacterActionState.Throwing)
            {
                StartOneShot(
                    GetThrowableActionData(),
                    GetThrowableThrowAnimationName(),
                    ActionPresentation.Attack,
                    throwableAimHoldTime);
                return;
            }
            if (evt.State == CharacterActionState.Attack1 || evt.State == CharacterActionState.Attack2 || evt.State == CharacterActionState.Attack3)
            {
                PlayStateAttack(evt.State);
            }
        }

        private void PlayStateAttack(CharacterActionState state)
        {
            var config = GameRoot.Instance?.Context?.Configs.GetItem(selectedItemId);
            if (config != null && config.CombatAttackKind == CombatAttackKind.Firearm)
            {
                // The release event has already been approved by CombatSystem
                // (including the magazine transaction) before Attack1 reaches
                // this view. Start from an Inspector-tunable point in the
                // authored shot clip rather than replaying its anticipation.
                var attack = pendingFirearmAttack;
                if (StartOneShot(
                    firearmActionData,
                    "gun -shot",
                    ActionPresentation.Attack,
                    gunShotStartTimeSeconds))
                {
                    ArmFirearmShotTiming(attack);
                }
                else
                {
                    ClearPendingFirearmShot();
                }
                return;
            }
            ClearPendingFirearmShot();
            if (config != null && config.CombatAttackKind != CombatAttackKind.Melee) { StartOneShot(GetThrowableActionData(), GetThrowableThrowAnimationName(), ActionPresentation.Attack); return; }
            var stage = state == CharacterActionState.Attack1 ? 1 : state == CharacterActionState.Attack2 ? 2 : 3;
            if (selectedItemId == FireAxeItemId) StartOneShot(stage == 1 ? fireAxeAttack1Data : stage == 2 ? fireAxeAttack2Data : fireAxeAttack3Data, stage == 1 ? "act1_both hands" : stage == 2 ? "act2 both hands" : "act3 both hands", ActionPresentation.Attack);
            else StartOneShot(GetMeleeActionData(selectedItemId), stage == 1 ? "act（single）1" : stage == 2 ? "act（single）2" : "act（single）3", ActionPresentation.Attack);
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
                GetMeleeActionData(weaponItemId),
                sharedMeleeComboStage == 1 ? "act（single）1" : sharedMeleeComboStage == 2 ? "act（single）2" : "act（single）3",
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

        private void HandleAttackStarted(AttackStartedEvent evt)
        {
            var attack = evt.Attack;
            if (attack == null || attack.AttackKind != CombatAttackKind.Firearm ||
                attack.AttackerId != CombatTargetIds.Player)
            {
                return;
            }

            // AttackStarted is emitted before CharacterSystem enters Attack1.
            // Cache the approved context here; the shot-frame event is emitted
            // only after the actual gun -shot track reaches its threshold.
            ClearPendingFirearmShot();
            pendingFirearmAttack = attack;
        }

        private void HandleCombatDamage(DamageAppliedEvent evt)
        {
            if (evt.TargetId != CombatTargetIds.Player || isDead)
            {
                return;
            }

            if (evt.Result.WasBlocked && character != null && character.HasShieldEquipped)
            {
                SpineEffectPlayer.TryPlayFollowing(
                    SpineEffectPlayer.ShieldBlockResource,
                    "animation",
                    transform,
                    new Vector3(facingRight ? 0.48f : -0.48f, 1.12f, 0f),
                    0.85f,
                    75);
                return;
            }

            // Non-zero shield damage is handled by CharacterDamageReceivedEvent
            // after CharacterSystem has accepted it.  A zero-damage shield hit
            // still reaches this handler for the block effect above.
        }

        private void HandleCharacterDamageReceived(CharacterDamageReceivedEvent evt)
        {
            if (isDead || evt.FinalDamage <= 0f)
            {
                return;
            }

            StartOneShot(armedHurtData, "hurt1", ActionPresentation.Hurt);

            SpineEffectPlayer.TryPlayFollowing(
                SpineEffectPlayer.PlayerHurtResource,
                equippedOffhand == OffhandType.Splint ? "gangjiaban hurt" : "hurt",
                transform,
                new Vector3(0f, 1.05f, 0f),
                1f,
                70);
        }

        private void HandleItemEffectRequested(CharacterItemEffectRequestedEvent evt)
        {
            var effect = evt.Effect;
            if (effect == null || isDead)
            {
                return;
            }

            if (effect.RestoreFullHealth || effect.HealthRestore > 0f)
            {
                SpineEffectPlayer.TryPlayFollowing(
                    SpineEffectPlayer.HealResource,
                    "animation",
                    transform,
                    new Vector3(0f, 0.95f, 0f),
                    0.55f,
                    65);
            }

            if (effect.RestoreFullStamina || effect.HasTimedModifier)
            {
                SpineEffectPlayer.TryPlayFollowing(
                    SpineEffectPlayer.BuffResource,
                    "animation",
                    transform,
                    new Vector3(0f, 1.05f, 0f),
                    0.65f,
                    64);
            }
        }

        private void HandleAimInput(AimInputEvent evt)
        {
            if (isDead || current == LocomotionState.Dodge) return;
            if (!evt.IsAiming)
            {
                hasFirearmAimWorldPosition = false;
                // 投掷瞄准由角色状态驱动；输入层每帧都会发布 AimInputEvent(false)，
                // 若在此打断，按住瞄准期间投掷姿势会立刻被切回待机。
                if (activeAction == ActionPresentation.Aim && presentedState != CharacterActionState.ThrowAiming) StopActionToLocomotion();
                return;
            }

            if (activeAction != ActionPresentation.None && activeAction != ActionPresentation.Aim) return;
            if (SelectedItemUses(CombatAttackKind.Firearm) && firearmActionData != null)
            {
                firearmAimWorldPosition = evt.PointerWorldPosition;
                hasFirearmAimWorldPosition = true;
                // gun-hold is the equipped firearm idle.  A primary Started
                // event switches to gun-ami exactly once and holds its final
                // frame until release/cancel.
                StartHeldOneShot(firearmActionData, "gun-ami", ActionPresentation.Aim, false);
                if (character != null) UpdateFacing(character.AimDirection);
            }
            else if (SelectedItemUses(CombatAttackKind.Throwable))
            {
                StartHeldOneShot(GetThrowableActionData(), GetThrowableAimAnimationName(), ActionPresentation.Aim);
            }
        }

        private void HandleReloadRequested(ReloadRequestedEvent evt)
        {
            var weaponConfig = evt.Weapon == null
                ? null
                : GameRoot.Instance?.Context?.Configs.GetItem(evt.Weapon.itemId);
            if (!isDead && firearmActionData != null &&
                (SelectedItemUses(CombatAttackKind.Firearm) ||
                 weaponConfig != null && weaponConfig.CombatAttackKind == CombatAttackKind.Firearm))
            {
                StartOneShot(firearmActionData, "gun-change bullet", ActionPresentation.Reload);
            }
        }

        private void HandleCharacterDied(CharacterDiedEvent evt)
        {
            isDead = true;
            CancelCurrentAction();
            UnsubscribeDodgeComplete();
            ResetDodgeVisualOffset();
            current = LocomotionState.Idle;
            SwitchSkeleton(bayonetCombatData, "death", false);
        }

        private void HandleDodgeRequested(DodgeRequestedEvent evt)
        {
            if (isDead) return;
            CancelCurrentAction();
            ResetDodgeVisualOffset();
            current = LocomotionState.Dodge;
            stateEnteredAt = Time.time;
            UnsubscribeDodgeComplete();
            var entry = SwitchSkeleton(equippedOffhand == OffhandType.Lantern ? lanternDodgeData : dodgeData, "dodge", false);
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
            ResetDodgeVisualOffset();
            SwitchToLocomotion();
        }

        private void HandleDodgeMotionProgress(DodgeMotionProgressEvent evt)
        {
            CacheDodgeVisualBasePosition();
            if (skeletonAnimation == null || !hasCachedDodgeVisualBasePosition)
            {
                return;
            }

            var progress = Mathf.Clamp01(evt.NormalizedProgress);
            var arcOffset = dodgeArcHeight * 4f * progress * (1f - progress);
            var position = dodgeVisualBasePosition;
            position.y += arcOffset;
            skeletonAnimation.transform.localPosition = position;
        }

        private bool StartOneShot(
            SkeletonDataAsset data,
            string animationName,
            ActionPresentation presentation,
            float startTime = 0f)
        {
            if (data == null || isDead || current == LocomotionState.Dodge) return false;
            CancelCurrentAction();
            activeAction = presentation;
            var entry = SwitchSkeleton(data, animationName, false);
            if (entry == null)
            {
                activeAction = ActionPresentation.None;
                SwitchToLocomotion();
                return false;
            }

            // Throw aiming and release are separate Spine animations. Starting
            // the release clip at the held timestamp avoids visibly replaying its
            // opening frames after the mouse button is released.
            SetTrackStartTime(entry, startTime);

            actionTrackEntry = entry;
            actionTrackEntry.Complete += HandleActionComplete;
            return true;
        }

        private void StartLoop(SkeletonDataAsset data, string animationName, ActionPresentation presentation)
        {
            if (data == null || isDead || current == LocomotionState.Dodge) return;
            if (activeAction == presentation && skeletonAnimation.skeletonDataAsset == data) return;
            CancelCurrentAction();
            activeAction = presentation;
            SwitchSkeleton(data, animationName, true);
        }

        /// <summary>
        /// Plays a preparation animation once and deliberately leaves its track
        /// active at the configured held pose, so holding input cannot restart or
        /// loop the wind-up.
        /// </summary>
        private void StartHeldOneShot(
            SkeletonDataAsset data,
            string animationName,
            ActionPresentation presentation,
            bool clampToThrowableHoldPose = true)
        {
            if (data == null || isDead || current == LocomotionState.Dodge) return;
            if (activeAction == presentation && skeletonAnimation.skeletonDataAsset == data) return;
            CancelCurrentAction();
            activeAction = presentation;
            var entry = SwitchSkeleton(data, animationName, false);
            if (entry == null)
            {
                activeAction = ActionPresentation.None;
                SwitchToLocomotion();
                return;
            }

            if (clampToThrowableHoldPose)
            {
                // The throwable wind-up is authored past the intended held pose.
                // Clamp this non-looping track to the measured pose instead of letting
                // it reach the clip's final frame.
                entry.AnimationEnd = Mathf.Clamp(
                    throwableAimHoldTime,
                    entry.AnimationStart,
                    entry.AnimationEnd);
            }

            // A non-looping TrackEntry holds its final pose while it remains on
            // the track. Keep it alive until the aim input explicitly changes
            // state, rather than letting Spine clear it at clip completion.
            entry.TrackEnd = float.MaxValue;
        }

        private void HandleActionComplete(TrackEntry trackEntry)
        {
            if (trackEntry != actionTrackEntry) return;

            // A very large frame can advance a track past the release-delay
            // target and fire Complete before LateUpdate gets a chance to poll it.
            // Consume the ready attack while the completed gun pose is still
            // applied, then publish exactly once before replacing the track.
            var completedFirearmAttack = TakeReadyFirearmShot(trackEntry);
            if (completedFirearmAttack != null)
            {
                PublishFirearmShotFrame(completedFirearmAttack);
            }

            var completedAction = activeAction;
            var completedState = presentedState;
            UnsubscribeActionComplete();
            activeAction = ActionPresentation.None;

            if (completedAction == ActionPresentation.Equip)
            {
                isUsingWalkingEquipAnimation = false;
                equipPlaybackDuration = 0f;
                PublishEquipAnimationState(false);
            }
            else
            {
                GameRoot.Instance?.Context?.Events.Publish(new CharacterActionAnimationCompletedEvent(completedState));
            }

            // 完成事件会同步驱动状态机。若状态机消费了缓冲输入并开始下一段攻击，
            // PlayStateAttack 已直接换好动画，此处不可再用 Idle 覆盖它。
            if (activeAction == ActionPresentation.None)
            {
                SwitchToLocomotion();
            }
        }

        private void StopActionToLocomotion()
        {
            CancelCurrentAction();
            SwitchToLocomotion();
        }

        private void CancelCurrentAction()
        {
            ClearPendingFirearmShot();
            if (activeAction == ActionPresentation.Equip)
            {
                isUsingWalkingEquipAnimation = false;
                equipPlaybackDuration = 0f;
                PublishEquipAnimationState(false);
            }
            UnsubscribeActionComplete();
            activeAction = ActionPresentation.None;
        }

        private void ArmFirearmShotTiming(AttackContext attack)
        {
            ClearPendingFirearmShot();
            if (attack == null || attack.AttackKind != CombatAttackKind.Firearm ||
                attack.AttackerId != CombatTargetIds.Player || actionTrackEntry == null)
            {
                return;
            }

            pendingFirearmAttack = attack;
            pendingFirearmShotTrackEntry = actionTrackEntry;
            pendingFirearmShotTargetTrackTime = GetFirearmShotTargetTrackTime(actionTrackEntry);

            var events = GameRoot.Instance?.Context?.Events;
            if (events != null && !firearmShotLifetimeRequested)
            {
                // CombatSystem's normal firearm grace window is shorter than
                // this release-to-shot delay. Extend only this approved attack
                // until the animation-driven shot frame can report its hit.
                events.Publish(new CombatAttackLifetimeRequestedEvent(
                    attack.AttackInstanceId,
                    GetFirearmShotDelay(actionTrackEntry) + FirearmShotLifetimeSafetySeconds));
                firearmShotLifetimeRequested = true;
            }

            // This also handles a zero delay or a start time already past the
            // configured target without waiting for another frame.
            TryPublishFirearmShotFrame();
        }

        private void TryPublishFirearmShotFrame()
        {
            var trackEntry = pendingFirearmShotTrackEntry;
            if (pendingFirearmAttack == null || trackEntry == null || firearmShotFramePublished)
            {
                return;
            }

            if (isDead || current == LocomotionState.Dodge || activeAction != ActionPresentation.Attack ||
                actionTrackEntry != trackEntry)
            {
                ClearPendingFirearmShot();
                return;
            }

            var attack = TakeReadyFirearmShot(trackEntry);
            if (attack != null)
            {
                PublishFirearmShotFrame(attack);
            }
        }

        private AttackContext TakeReadyFirearmShot(TrackEntry trackEntry)
        {
            if (pendingFirearmAttack == null || pendingFirearmShotTrackEntry != trackEntry ||
                firearmShotFramePublished || !IsFirearmShotReady(trackEntry))
            {
                return null;
            }

            var attack = pendingFirearmAttack;
            firearmShotFramePublished = true;
            pendingFirearmAttack = null;
            pendingFirearmShotTrackEntry = null;
            pendingFirearmShotTargetTrackTime = 0f;
            firearmShotLifetimeRequested = false;
            return attack;
        }

        private bool IsFirearmShotReady(TrackEntry trackEntry)
        {
            if (trackEntry == null || float.IsNaN(trackEntry.TrackTime) ||
                float.IsInfinity(trackEntry.TrackTime))
            {
                return false;
            }

            return trackEntry.TrackTime + 0.0001f >= pendingFirearmShotTargetTrackTime;
        }

        private float GetFirearmShotDelay(TrackEntry trackEntry)
        {
            if (trackEntry == null || float.IsNaN(trackEntry.TrackTime) ||
                float.IsInfinity(trackEntry.TrackTime))
            {
                return 0f;
            }

            var remainingTrackTime = Mathf.Max(0f, pendingFirearmShotTargetTrackTime - trackEntry.TrackTime);
            var timeScale = GetAbsoluteTrackTimeScale(trackEntry);
            if (timeScale <= 0.0001f)
            {
                return 0f;
            }
            return remainingTrackTime / timeScale;
        }

        private float GetFirearmShotTargetTrackTime(TrackEntry trackEntry)
        {
            if (trackEntry == null || float.IsNaN(trackEntry.TrackTime) ||
                float.IsInfinity(trackEntry.TrackTime))
            {
                return 0f;
            }

            var clipDuration = Mathf.Max(0f, trackEntry.AnimationEnd - trackEntry.AnimationStart);
            var startTrackTime = Mathf.Clamp(trackEntry.TrackTime, 0f, clipDuration);
            var delaySeconds = Mathf.Max(0f, firearmShotDelayAfterReleaseSeconds);
            var timeScale = GetAbsoluteTrackTimeScale(trackEntry);
            var delayTrackTime = timeScale > 0.0001f ? delaySeconds * timeScale : 0f;
            return Mathf.Clamp(startTrackTime + delayTrackTime, 0f, clipDuration);
        }

        private static float GetAbsoluteTrackTimeScale(TrackEntry trackEntry)
        {
            if (trackEntry == null)
            {
                return 0f;
            }

            var timeScale = Mathf.Abs(trackEntry.TimeScale);
            return float.IsNaN(timeScale) || float.IsInfinity(timeScale) ? 0f : timeScale;
        }

        private void PublishFirearmShotFrame(AttackContext attack)
        {
            if (attack == null) return;
            GameRoot.Instance?.Context?.Events.Publish(new FirearmShotFrameEvent(attack));
        }

        private void ClearPendingFirearmShot()
        {
            pendingFirearmAttack = null;
            pendingFirearmShotTrackEntry = null;
            pendingFirearmShotTargetTrackTime = 0f;
            firearmShotFramePublished = false;
            firearmShotLifetimeRequested = false;
        }

        private bool StartEquipAnimation(bool walking)
        {
            if (weaponEquipData == null || isDead || current == LocomotionState.Dodge) return false;

            CancelCurrentAction();
            activeAction = ActionPresentation.Equip;
            isUsingWalkingEquipAnimation = walking;
            equipPlaybackDuration = GetAnimationDuration(weaponEquipData, "zhuangbei");
            var animationName = walking ? "zhuangbei+walk" : "zhuangbei";
            var entry = SwitchSkeleton(weaponEquipData, animationName, false);
            if (entry == null)
            {
                activeAction = ActionPresentation.None;
                equipPlaybackDuration = 0f;
                SwitchToLocomotion();
                return false;
            }

            if (equipPlaybackDuration <= 0f) equipPlaybackDuration = entry.AnimationEnd;
            ConfigureEquipTrack(entry, 0f);
            return true;
        }

        private static void SetTrackStartTime(TrackEntry entry, float requestedStartTime)
        {
            if (entry == null || !(requestedStartTime > 0f))
            {
                return;
            }

            var clipDuration = Mathf.Max(0f, entry.AnimationEnd - entry.AnimationStart);
            if (clipDuration <= 0f)
            {
                entry.TrackTime = 0f;
                return;
            }

            // TrackTime is elapsed seconds from AnimationStart, not an
            // absolute key time. Keep it strictly before the clip end so the
            // non-looping entry still gets one update/Complete callback.
            var safeEnd = Mathf.Max(0f, clipDuration - 0.0001f);
            var elapsedSeconds = Mathf.Clamp(requestedStartTime, 0f, safeEnd);
            entry.TrackTime = elapsedSeconds;
            // Do not replay event keys before the requested starting point when
            // the asset is later given a release event timeline.
            entry.AnimationLast = entry.AnimationStart + elapsedSeconds;
        }

        private void SwitchEquipAnimation(bool walking)
        {
            if (isUsingWalkingEquipAnimation == walking || weaponEquipData == null) return;

            // Both clips are two presentations of one logical equip action. Copy
            // normalized progress and scale the target clip against the canonical
            // duration so walking/stopping never restarts or extends the action.
            var progress = GetNormalizedTrackProgress(actionTrackEntry);

            UnsubscribeActionComplete();
            var entry = SwitchSkeleton(weaponEquipData, walking ? "zhuangbei+walk" : "zhuangbei", false);
            if (entry == null)
            {
                activeAction = ActionPresentation.None;
                equipPlaybackDuration = 0f;
                PublishEquipAnimationState(false);
                SwitchToLocomotion();
                return;
            }

            isUsingWalkingEquipAnimation = walking;
            ConfigureEquipTrack(entry, progress);
        }

        private void ConfigureEquipTrack(TrackEntry entry, float normalizedProgress)
        {
            var clipDuration = Mathf.Max(0f, entry.AnimationEnd - entry.AnimationStart);
            if (equipPlaybackDuration <= 0f) equipPlaybackDuration = clipDuration;
            if (clipDuration > 0f && equipPlaybackDuration > 0f)
            {
                entry.TimeScale = clipDuration / equipPlaybackDuration;
                entry.TrackTime = Mathf.Clamp01(normalizedProgress) * clipDuration;
                // Switching equip poses must not replay earlier foot contacts.
                entry.AnimationLast = entry.AnimationStart + entry.TrackTime;
            }

            actionTrackEntry = entry;
            actionTrackEntry.Complete += HandleActionComplete;
        }

        private static float GetNormalizedTrackProgress(TrackEntry entry)
        {
            if (entry == null) return 0f;
            var duration = entry.AnimationEnd - entry.AnimationStart;
            return duration > 0f ? Mathf.Clamp01(entry.TrackTime / duration) : 0f;
        }

        private static float GetAnimationDuration(SkeletonDataAsset data, string animationName)
        {
            var skeletonData = data != null ? data.GetSkeletonData(false) : null;
            var animation = skeletonData?.FindAnimation(animationName);
            return animation != null ? animation.Duration : 0f;
        }

        private static void PublishEquipAnimationState(bool isPlaying)
        {
            GameRoot.Instance?.Context?.Events.Publish(new CharacterEquipAnimationStateChangedEvent(isPlaying));
        }

        private bool SelectedItemUses(CombatAttackKind attackKind)
        {
            var config = GameRoot.Instance?.Context?.Configs.GetItem(selectedItemId);
            return config != null && config.CombatAttackKind == attackKind;
        }

        private SkeletonDataAsset GetMeleeActionData(int itemId)
        {
            return itemId == BayonetItemId && updatedBayonetAttackData != null
                ? updatedBayonetAttackData
                : oneHandedAttackData;
        }

        private SkeletonDataAsset GetThrowableActionData()
        {
            return updatedGrenadeThrowData != null ? updatedGrenadeThrowData : bayonetCombatData;
        }

        private string GetThrowableAimAnimationName()
        {
            return ResolveAnimationName(GetThrowableActionData(), "throw_aim", "throw1");
        }

        private string GetThrowableThrowAnimationName()
        {
            return ResolveAnimationName(GetThrowableActionData(), "throw", "throw2");
        }

        private static string ResolveAnimationName(SkeletonDataAsset data, string preferred, string fallback)
        {
            var skeletonData = data != null ? data.GetSkeletonData(false) : null;
            if (skeletonData?.FindAnimation(preferred) != null) return preferred;
            if (skeletonData?.FindAnimation(fallback) != null) return fallback;
            return preferred;
        }

        private void SwitchToLocomotion()
        {
            if (isDead) return;
            if (current == LocomotionState.Dodge) PublishDodgeAnimationState(false);
            UnsubscribeDodgeComplete();
            current = LocomotionState.Idle;
            stateEnteredAt = Time.time;
            SwitchIdleAnimation();
        }

        private void SwitchIdleAnimation()
        {
            if (SelectedItemUses(CombatAttackKind.Firearm) && firearmActionData != null)
            {
                SwitchSkeleton(firearmActionData, "gun-hold", true);
                return;
            }

            SwitchSkeleton(idleData, "idle", true);
        }

        private void UpdateFirearmAim(Vector2 targetWorldPosition)
        {
            var skeleton = skeletonAnimation != null ? skeletonAnimation.skeleton : null;
            var aimBone = skeleton?.FindBone(firearmAimBoneName);
            if (aimBone == null || skeletonAnimation == null || !IsFinite(targetWorldPosition)) return;

            // Resolve the direction from the actual aim pivot, not from the
            // character root. Then inverse-transform it through the parent
            // bone matrix so both the mirrored left-facing skeleton and the
            // Player's non-uniform parent scale are handled exactly once.
            var pivotWorld = skeletonAnimation.transform.TransformPoint(
                new Vector3(aimBone.WorldX, aimBone.WorldY, 0f));
            var worldDirection = targetWorldPosition - (Vector2)pivotWorld;
            if (worldDirection.sqrMagnitude <= 0.0001f) return;

            var skeletonDirection = skeletonAnimation.transform.InverseTransformVector(
                new Vector3(worldDirection.x, worldDirection.y, 0f));
            var parent = aimBone.Parent;
            if (parent != null)
            {
                var determinant = parent.A * parent.D - parent.B * parent.C;
                if (Mathf.Abs(determinant) <= 0.000001f) return;

                skeletonDirection = new Vector3(
                    (skeletonDirection.x * parent.D - skeletonDirection.y * parent.B) / determinant,
                    (skeletonDirection.y * parent.A - skeletonDirection.x * parent.C) / determinant,
                    0f);
            }

            if (skeletonDirection.sqrMagnitude <= 0.0001f) return;
            aimBone.Rotation = Mathf.Atan2(skeletonDirection.y, skeletonDirection.x) * Mathf.Rad2Deg
                               + firearmAimAngleOffset;
            skeleton.UpdateWorldTransform();
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }

        private void CacheDodgeVisualBasePosition()
        {
            if (hasCachedDodgeVisualBasePosition || skeletonAnimation == null)
            {
                return;
            }

            dodgeVisualBasePosition = skeletonAnimation.transform.localPosition;
            hasCachedDodgeVisualBasePosition = true;
        }

        private void ResetDodgeVisualOffset()
        {
            CacheDodgeVisualBasePosition();
            if (skeletonAnimation != null && hasCachedDodgeVisualBasePosition)
            {
                skeletonAnimation.transform.localPosition = dodgeVisualBasePosition;
            }
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
            entry.Event += HandleFootstepEvent;
            ApplyFacing();
            return entry;
        }

        private void HandleFootstepEvent(TrackEntry entry, Spine.Event evt)
        {
            if (evt.Data.Name != "footstep" || !isActiveAndEnabled || isDead || Time.timeScale <= 0f ||
                skeletonAnimation?.state?.GetCurrent(0) != entry || character == null ||
                character.MoveDirection.sqrMagnitude <= 0.0001f) return;
            GameRoot.Instance?.Context?.Events.Publish(new PlayerFootstepEvent(evt.Int == 1));
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
