using System;
using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Character.Data;
using MemorialArchive.Gameplay.Character.Logic;
using MemorialArchive.Gameplay.Combat.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemorialArchive.Framework.Audio
{
    public sealed class AudioSystem : IGameSystem
    {
        private readonly List<Action> unsubscribe = new List<Action>();
        private GameContext context;
        private AudioCatalog catalog;
        private int equippedItem;
        private Vector2 previousPosition;
        private bool hasPosition;
        private float lastMovementTime = float.NegativeInfinity;
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
            if (catalog == null) Debug.LogError("Missing Resources/Audio/GameAudioCatalog.");
            Listen<SceneLoadedEvent>(e => SetScene(e.SceneId));
            Listen<PlayerPositionChangedEvent>(OnPosition);
            Listen<PlayerFootstepEvent>(OnFootstep);
            Listen<DodgeRequestedEvent>(e => Play("sfx_player_dodge"));
            Listen<CharacterDiedEvent>(e => Play("sfx_player_death"));
            Listen<CharacterDamageReceivedEvent>(e => {
                var character = GameRoot.Instance?.GetSystem<CharacterSystem>();
                if (character != null && character.Data.health > 0 && (e.FinalDamage > 0 || e.WasBlocked))
                    Play(e.WasBlocked ? "sfx_player_block_hit" : "sfx_player_hurt");
            });
            Listen<CharacterEquipmentChangedEvent>(e => {
                if (e.PrimaryItemId != equippedItem && e.PrimaryItemId > 0) Play("sfx_player_equip_" + Weapon(e.PrimaryItemId));
                equippedItem = e.PrimaryItemId;
            });
            Listen<AttackStartedEvent>(e => {
                if (e.Attack.AttackKind == CombatAttackKind.Melee) Play("sfx_player_attack_" + Weapon(e.Attack.WeaponItemId));
                else if (e.Attack.AttackKind == CombatAttackKind.Throwable) Play("sfx_player_throw");
            });
            Listen<FirearmShotFrameEvent>(e => Play("sfx_player_shoot_" + Weapon(e.Attack.WeaponItemId)));
            Listen<LanternLitChangedEvent>(e => Play("sfx_scene_lantern_switch"));
            Listen<SelectedItemChangedEvent>(e => { if (e.Item != null) Play("sfx_ui_inventory_select"); });
            Listen<PanelOpenedEvent>(OnPanel);
            Listen<DialogueAdvancePressedEvent>(e => Play("sfx_ui_dialogue_click"));
            Listen<NoteUnlockedEvent>(e => Play("sfx_scene_note_pickup"));
            Listen<DoorUnlockedEvent>(e => Play("sfx_scene_key_unlock"));
            SetScene(SceneManager.GetActiveScene().name);
        }
        private string Weapon(int id)
        {
            var name = context.Configs.GetItem(id)?.ItemName ?? "";
            if (name.Contains("匕首")) return "dagger";
            if (name.Contains("刺刀")) return "bayonet";
            if (name.Contains("斧")) return "axe";
            if (name.Contains("手枪")) return "pistol";
            return id.ToString();
        }
        private void Listen<T>(Action<T> handler)
        {
            context.Events.Subscribe(handler);
            unsubscribe.Add(() => context.Events.Unsubscribe(handler));
        }
        private void OnPanel(PanelOpenedEvent e)
        {
            switch (e.PanelId)
            {
                case PanelId.Inventory: Play("sfx_ui_inventory_open"); break;
                case PanelId.Container: Play("sfx_scene_container_open"); break;
                case PanelId.Diary: Play("sfx_ui_diary_open"); break;
                case PanelId.Map: Play("sfx_ui_map_open"); break;
                case PanelId.System:
                case PanelId.Settings: Play("sfx_ui_settings"); break;
            }
        }
        private void SetScene(string scene)
        {
            Playback.StopSceneVoices(); hasPosition = false; lastMovementTime = float.NegativeInfinity;
            Surface = "wood";
            if (catalog?.scenes == null) return;
            foreach (var profile in catalog.scenes)
            {
                if (profile.scene != scene) continue;
                Surface = profile.surface;
                Playback.Play(profile.ambience); Playback.Play(profile.music);
                break;
            }
        }
        private void OnPosition(PlayerPositionChangedEvent e)
        {
            Playback.ListenerPosition = e.Position;
            float distance = hasPosition ? Vector2.Distance(previousPosition, e.Position) : 0;
            previousPosition = e.Position; hasPosition = true;
            lastMovementTime = distance > 0.0001f && distance < 1f ? Time.time : float.NegativeInfinity;
        }
        private void OnFootstep(PlayerFootstepEvent e)
        {
            var character = GameRoot.Instance?.GetSystem<CharacterSystem>();
            if (character == null || character.ActionState == CharacterActionState.Dodging || character.Data.health <= 0 ||
                character.MoveDirection.sqrMagnitude < 0.001f || Time.timeScale <= 0 ||
                Time.time - lastMovementTime > 0.1f) return;
            Play("sfx_player_" + (e.Running ? "run_" : "footstep_") + Surface);
        }
        public void Dispose()
        {
            foreach (var remove in unsubscribe) remove();
            unsubscribe.Clear();
            if (Playback != null) UnityEngine.Object.Destroy(Playback.gameObject);
            Playback = null; context = null;
        }
    }
}
