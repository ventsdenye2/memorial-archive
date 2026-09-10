using System;
using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Character.Data;
using MemorialArchive.Gameplay.Character.Logic;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Monster.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemorialArchive.Framework.Audio
{
    [Serializable]
    public sealed class AudioSaveData
    {
        public bool storyReady;
        public bool explorationStarted;
        public bool explorationCompleted;
        public List<string> visitedExplorationScenes = new List<string>();
    }

    /// <summary>
    /// Owns audio routing decisions.  Audio clips remain data in AudioCatalog;
    /// gameplay systems only publish facts and this system maps those facts to
    /// cues. Scene ambience is paused while music is active and resumes from
    /// the same playback position when the music ends.
    /// </summary>
    public sealed class AudioSystem : IGameSystem, ITickableSystem, ISaveModule, INewGameResettable
    {
        private const int BayonetItemId = 1002;
        private const int PistolItemId = 1007;
        private const int GrenadeItemId = 1010;
        private const float BattleReleaseDelaySeconds = 1f;

        private readonly List<Action> unsubscribe = new List<Action>();
        private readonly HashSet<string> lockedMonsters = new HashSet<string>();
        private readonly AudioSaveData saveData = new AudioSaveData();
        private GameContext context;
        private AudioCatalog catalog;
        private string currentScene;
        private string sceneAmbience;
        private string sceneMusic;
        private string activeMusicCue;
        private string activeAmbienceCue;
        private float battleUntil = float.NegativeInfinity;
        private bool pistolCockPending;
        private float pistolCockAt;
        private int equippedItem;

        public string ModuleKey => "audio";
        public string Surface { get; private set; } = "wood";
        public AudioPlayback Playback { get; private set; }
        public static AudioSystem Current => GameRoot.Instance?.GetSystem<AudioSystem>();

        public static void Play(string id) => Current?.Playback?.Play(id);

        public void Initialize(GameContext gameContext)
        {
            context = gameContext;
            catalog = Resources.Load<AudioCatalog>("Audio/GameAudioCatalog");

            var host = new GameObject("Game Audio");
            host.transform.SetParent(GameRoot.Instance.transform);
            Playback = host.AddComponent<AudioPlayback>();
            Playback.Initialize(catalog);
            if (catalog == null)
            {
                Debug.LogError("Missing Resources/Audio/GameAudioCatalog.");
            }

            Listen<Stage1GameplayStartedEvent>(OnStage1GameplayStarted);
            Listen<SceneLoadedEvent>(OnSceneLoaded);
            Listen<PlayerPositionChangedEvent>(e => Playback.ListenerPosition = e.Position);
            Listen<PlayerFootstepEvent>(OnFootstep);
            Listen<DodgeRequestedEvent>(e => Play("sfx_player_dodge"));
            Listen<CharacterDiedEvent>(e => Play("sfx_player_death"));
            Listen<DamageAppliedEvent>(OnDamageApplied);
            Listen<CharacterDamageReceivedEvent>(OnCharacterDamageReceived);
            Listen<CharacterEquipmentChangedEvent>(OnEquipmentChanged);
            Listen<AttackStartedEvent>(OnAttackStarted);
            Listen<FirearmShotFrameEvent>(OnFirearmShot);
            Listen<ReloadRequestedEvent>(OnReloadRequested);
            Listen<GrenadeExplodedEvent>(OnGrenadeExploded);
            Listen<MonsterStateChangedEvent>(OnMonsterStateChanged);
            Listen<MonsterDiedEvent>(OnMonsterDied);
            Listen<ItemEffectAppliedEvent>(OnItemEffectApplied);
            Listen<LanternLitChangedEvent>(e => Play("sfx_scene_lantern_switch"));
            Listen<SelectedItemChangedEvent>(e => { if (e.Item != null) Play("sfx_ui_inventory_select"); });
            Listen<PanelOpenedEvent>(OnPanelOpened);
            Listen<DialogueAdvancePressedEvent>(e => Play("sfx_ui_dialogue_click"));
            Listen<NoteUnlockedEvent>(e => Play("sfx_scene_note_pickup"));
            Listen<DoorUnlockedEvent>(e => Play("sfx_scene_key_unlock"));

            SetScene(SceneManager.GetActiveScene().name);
        }

        public void Tick(float deltaTime)
        {
            if (pistolCockPending && Time.unscaledTime >= pistolCockAt)
            {
                pistolCockPending = false;
                Play("sfx_player_cock_pistol");
            }

            RefreshRouting();
        }

        public object CaptureSaveData() => saveData;

        public void RestoreSaveData(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var restored = JsonUtility.FromJson<AudioSaveData>(json);
            if (restored == null)
            {
                return;
            }

            saveData.storyReady = restored.storyReady;
            saveData.explorationStarted = restored.explorationStarted;
            saveData.explorationCompleted = restored.explorationCompleted;
            saveData.visitedExplorationScenes.Clear();
            if (restored.visitedExplorationScenes != null)
            {
                saveData.visitedExplorationScenes.AddRange(restored.visitedExplorationScenes);
            }

            RefreshRouting();
        }

        public void ResetForNewGame()
        {
            saveData.storyReady = false;
            saveData.explorationStarted = false;
            saveData.explorationCompleted = false;
            saveData.visitedExplorationScenes.Clear();
            lockedMonsters.Clear();
            battleUntil = float.NegativeInfinity;
            pistolCockPending = false;
            equippedItem = 0;
            activeMusicCue = null;
            activeAmbienceCue = null;
            RefreshRouting();
        }

        public void Dispose()
        {
            foreach (var remove in unsubscribe)
            {
                remove();
            }

            unsubscribe.Clear();
            lockedMonsters.Clear();
            if (Playback != null)
            {
                UnityEngine.Object.Destroy(Playback.gameObject);
            }

            Playback = null;
            context = null;
        }

        private void Listen<T>(Action<T> handler)
        {
            context.Events.Subscribe(handler);
            unsubscribe.Add(() => context.Events.Unsubscribe(handler));
        }

        private void OnStage1GameplayStarted(Stage1GameplayStartedEvent evt)
        {
            saveData.storyReady = true;
            if (IsExplorationFloor(currentScene))
            {
                saveData.explorationStarted = true;
            }

            RefreshRouting();
        }

        private void OnSceneLoaded(SceneLoadedEvent evt)
        {
            lockedMonsters.Clear();
            battleUntil = float.NegativeInfinity;
            pistolCockPending = false;

            if (saveData.storyReady && IsExplorationFloor(evt.SceneId))
            {
                saveData.explorationStarted = true;
            }

            if (saveData.explorationStarted && IsCompletionScene(evt.SceneId) &&
                !saveData.visitedExplorationScenes.Contains(evt.SceneId))
            {
                saveData.visitedExplorationScenes.Add(evt.SceneId);
                if (AllCompletionScenesVisited())
                {
                    saveData.explorationCompleted = true;
                }
            }

            SetScene(evt.SceneId);
        }

        private void SetScene(string scene)
        {
            currentScene = scene;
            Playback?.StopSceneVoices();
            activeMusicCue = null;
            activeAmbienceCue = null;
            Surface = "wood";
            sceneAmbience = string.Empty;
            sceneMusic = string.Empty;

            if (catalog?.scenes != null)
            {
                foreach (var profile in catalog.scenes)
                {
                    if (profile == null || profile.scene != scene)
                    {
                        continue;
                    }

                    Surface = string.IsNullOrEmpty(profile.surface) ? "wood" : profile.surface;
                    sceneAmbience = profile.ambience;
                    sceneMusic = profile.music;
                    break;
                }
            }

            RefreshRouting();
        }

        private void RefreshRouting()
        {
            if (Playback == null)
            {
                return;
            }

            var music = ResolveMusicCue();
            var ambience = sceneAmbience;

            if (music != activeMusicCue)
            {
                Playback.StopBus(AudioBus.Music);
                activeMusicCue = music;
                if (!string.IsNullOrEmpty(music))
                {
                    Playback.Play(music);
                }
            }

            if (ambience != activeAmbienceCue)
            {
                Playback.StopBus(AudioBus.Ambience);
                activeAmbienceCue = ambience;
                if (!string.IsNullOrEmpty(ambience))
                {
                    Playback.Play(ambience);
                }
            }

            // Keep the ambience voice alive so it can resume at its previous
            // position after battle or other music has finished.
            Playback.SetBusPaused(AudioBus.Ambience, !string.IsNullOrEmpty(music));
        }

        private string ResolveMusicCue()
        {
            if (currentScene == "MainMenu")
            {
                return sceneMusic;
            }

            if (IsBattleActive())
            {
                return catalog?.battleMusic;
            }

            if (saveData.explorationStarted && !saveData.explorationCompleted && IsExplorationScene(currentScene))
            {
                return catalog?.explorationMusic;
            }

            return string.Empty;
        }

        private bool IsBattleActive()
        {
            if (battleUntil > Time.unscaledTime)
            {
                return true;
            }

            return lockedMonsters.Count > 0;
        }

        private void OnMonsterStateChanged(MonsterStateChangedEvent evt)
        {
            if (string.IsNullOrEmpty(evt.MonsterInstanceId))
            {
                return;
            }

            if (evt.State == MonsterActionState.Dead || evt.State == MonsterActionState.Idle)
            {
                var wasLocked = lockedMonsters.Remove(evt.MonsterInstanceId);
                if (wasLocked)
                {
                    battleUntil = Time.unscaledTime + BattleReleaseDelaySeconds;
                }
            }
            else
            {
                if (IsEngagedState(evt.State))
                {
                    lockedMonsters.Add(evt.MonsterInstanceId);
                    // The state event is the single source of truth for target
                    // acquisition. Attacks, damage, and player actions do not
                    // extend battle music on their own.
                    battleUntil = Time.unscaledTime + BattleReleaseDelaySeconds;
                }
            }

            RefreshRouting();
        }

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.MonsterInstanceId))
            {
                var wasLocked = lockedMonsters.Remove(evt.MonsterInstanceId);
                if (wasLocked)
                {
                    battleUntil = Time.unscaledTime + BattleReleaseDelaySeconds;
                }
            }

            RefreshRouting();
        }

        private static bool IsEngagedState(MonsterActionState state) =>
            state == MonsterActionState.Chasing || state == MonsterActionState.Attacking ||
            state == MonsterActionState.AttackCooldown;

        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (evt.TargetId == CombatTargetIds.Player && evt.Result.WasBlocked)
            {
                Play("sfx_player_shield_block_hit");
            }
        }

        private void OnCharacterDamageReceived(CharacterDamageReceivedEvent evt)
        {
            if (evt.WasBlocked || evt.FinalDamage <= 0f)
            {
                return;
            }

            var character = GameRoot.Instance?.GetSystem<CharacterSystem>();
            Play(character != null && character.HasSplintEquipped
                ? "sfx_player_armor_hit"
                : "sfx_player_hurt");
        }

        private void OnEquipmentChanged(CharacterEquipmentChangedEvent evt)
        {
            if (evt.PrimaryItemId == BayonetItemId && equippedItem != BayonetItemId)
            {
                Play("sfx_player_equip_bayonet");
            }

            equippedItem = evt.PrimaryItemId;
        }

        private void OnAttackStarted(AttackStartedEvent evt)
        {
            var attack = evt.Attack;
            if (attack == null)
            {
                return;
            }

            if (attack.AttackKind == CombatAttackKind.Melee)
            {
                if (attack.WeaponItemId == BayonetItemId)
                {
                    Play("sfx_player_attack_bayonet");
                }
                else if (attack.WeaponItemId == 1001)
                {
                    Play("sfx_player_attack_dagger");
                }
            }
            else if (attack.AttackKind == CombatAttackKind.Throwable)
            {
                Play("sfx_player_throw");
                if (attack.WeaponItemId == GrenadeItemId)
                {
                    Play("sfx_player_grenade_pin");
                }

            }
        }

        private void OnFirearmShot(FirearmShotFrameEvent evt)
        {
            if (evt.Attack == null || evt.Attack.WeaponItemId != PistolItemId)
            {
                return;
            }

            Play("sfx_player_shoot_pistol");
            pistolCockPending = true;
            pistolCockAt = Time.unscaledTime + 0.14f;
        }

        private void OnReloadRequested(ReloadRequestedEvent evt)
        {
            if (evt.Weapon != null && evt.Weapon.itemId == PistolItemId)
            {
                Play("sfx_player_reload_pistol");
            }
        }

        private void OnGrenadeExploded(GrenadeExplodedEvent evt)
        {
            Play("sfx_player_grenade_explode");
        }

        private void OnItemEffectApplied(ItemEffectAppliedEvent evt)
        {
            switch (evt.ItemId)
            {
                case 1012: Play("sfx_item_drink_soda_water"); break;
                case 1013: Play("sfx_item_drink_tea"); break;
                case 1014: Play("sfx_item_drink_whiskey"); break;
                case 1015: Play("sfx_item_food_beef_jerky"); break;
                case 1016: Play("sfx_item_food_toffee"); break;
                case 1017: Play("sfx_item_bandage"); break;
                case 1018: Play("sfx_item_rum_medicine"); break;
                case 1019: Play("sfx_item_laudanum"); break;
            }
        }

        private void OnPanelOpened(PanelOpenedEvent evt)
        {
            switch (evt.PanelId)
            {
                case PanelId.Inventory: Play("sfx_ui_inventory_open"); break;
                case PanelId.Container: Play("sfx_scene_container_open"); break;
                case PanelId.Diary: Play("sfx_ui_diary_open"); break;
                case PanelId.Map: Play("sfx_ui_map_open"); break;
                case PanelId.System:
                case PanelId.Settings: Play("sfx_ui_settings"); break;
            }
        }

        private void OnFootstep(PlayerFootstepEvent evt)
        {
            var character = GameRoot.Instance?.GetSystem<CharacterSystem>();
            if (character == null || character.ActionState == CharacterActionState.Dodging ||
                character.Data.health <= 0 || character.MoveDirection.sqrMagnitude < 0.001f ||
                Time.timeScale <= 0f)
            {
                return;
            }

            var heavy = character.Data.health <= 1;
            var prefix = heavy
                ? (evt.Running ? "sfx_player_heavy_move_run" : "sfx_player_heavy_move_walk")
                : (evt.Running ? "sfx_player_run" : "sfx_player_footstep");
            Play(prefix + (heavy ? string.Empty : "_" + Surface));
        }

        private bool IsExplorationFloor(string scene) => scene == "Floor_2F" || scene == "Floor_3F";

        private bool IsExplorationScene(string scene)
        {
            if (catalog?.explorationScenes == null)
            {
                return false;
            }

            foreach (var candidate in catalog.explorationScenes)
            {
                if (candidate == scene)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsCompletionScene(string scene)
        {
            if (catalog?.explorationCompletionScenes == null)
            {
                return false;
            }

            foreach (var candidate in catalog.explorationCompletionScenes)
            {
                if (candidate == scene)
                {
                    return true;
                }
            }

            return false;
        }

        private bool AllCompletionScenesVisited()
        {
            if (catalog?.explorationCompletionScenes == null || catalog.explorationCompletionScenes.Length == 0)
            {
                return false;
            }

            foreach (var scene in catalog.explorationCompletionScenes)
            {
                if (!saveData.visitedExplorationScenes.Contains(scene))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
