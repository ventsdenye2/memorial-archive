using System;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Guide.Config;
using MemorialArchive.Gameplay.Inventory.Logic;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Item.Config;
using MemorialArchive.Gameplay.Lighting.Logic;
using MemorialArchive.Gameplay.Story.Logic;
using MemorialArchive.Gameplay.Monster.Data;
using MemorialArchive.Gameplay.Combat.Data;
using UnityEngine;

namespace MemorialArchive.Gameplay.Guide.Logic
{
    /// <summary>Scene teaching milestones; facts come from inventory, lighting, narrative and combat owners.</summary>
    public sealed class GuideFlowSystem : IGameSystem, ITickableSystem, ISaveModule, INewGameResettable
    {
        [Serializable] public sealed class Progress
        {
            public bool keyTakenInTreatment;
            public int combatPhase; // 0 not started, 1 approach, 2 dodge, 3 equip, 4 fight, 5 finished
            public bool obstacleCleared;
            public bool diaryCollected;
            public bool mapUnlocked;
            public float darkSeconds;
            public int encounterHealth = 100;
            public Vector2 encounterPosition;
            public bool hasEncounterPosition;
        }
        private Progress progress = new Progress();
        private GameContext context;
        private GuideSystem guide;
        private InventorySystem inventory;
        private LightingSystem lighting;
        private NarrativeSystem narrative;
        private string scene;
        private Vector2 playerPosition;
        private bool hasPosition;
        private float entryDelay;
        private int sceneReadyFrame;
        private bool dodgePending;
        private bool deathPending;
        public string ModuleKey => "guide_flow";
        public GuidePresentationConfig Config => guide?.Config;
        public int EncounterHealth => Mathf.Max(1, progress.encounterHealth);
        public Vector2 EncounterPosition => progress.hasEncounterPosition ? progress.encounterPosition : playerPosition + Vector2.right * 4;
        public void RecordEncounterPosition(Vector2 position) { progress.encounterPosition = position; progress.hasEncounterPosition = true; }
        public bool AwaitingWeaponSelection => NeedsEncounter && progress.combatPhase == 3;
        public bool MapUnlocked => progress.mapUnlocked;
        public bool NeedsObstacle => scene == "FrontHall" && !progress.obstacleCleared;
        public bool NeedsDiary => scene == "FrontHall" && guide.HasCompleted("light_activated") && !progress.diaryCollected;
        public bool NeedsEncounter => scene == Config?.encounterScene && progress.combatPhase > 0 && progress.combatPhase < 5;
        public bool OwnsDarknessFailure => scene == "FrontHall" && !guide.HasCompleted("light_activated");
        public Vector2 PlayerPosition => playerPosition;
        public bool HasPosition => hasPosition && Time.frameCount > sceneReadyFrame;

        public void Initialize(GameContext value)
        {
            context = value;
            guide = GameRoot.Instance.GetSystem<GuideSystem>();
            inventory = GameRoot.Instance.GetSystem<InventorySystem>();
            lighting = GameRoot.Instance.GetSystem<LightingSystem>();
            narrative = GameRoot.Instance.GetSystem<NarrativeSystem>();
            context.Events.Subscribe<SceneLoadedEvent>(OnScene);
            context.Events.Subscribe<PlayerPositionChangedEvent>(OnPosition);
            context.Events.Subscribe<PanelOpenedEvent>(OnPanel);
            context.Events.Subscribe<InteractionFocusChangedEvent>(OnFocus);
            context.Events.Subscribe<RegionLightsStateChangedEvent>(OnRegion);
            context.Events.Subscribe<InventoryChangedEvent>(OnInventory);
            context.Events.Subscribe<NoteUnlockedEvent>(OnNote);
            context.Events.Subscribe<GuideStepCompletedEvent>(OnPage);
            context.Events.Subscribe<MonsterStateChangedEvent>(OnMonsterState);
            context.Events.Subscribe<DodgeRequestedEvent>(OnDodge);
            context.Events.Subscribe<MonsterDiedEvent>(OnMonsterDied);
            context.Events.Subscribe<MonsterDamagedEvent>(OnMonsterDamaged);
            context.Events.Subscribe<CharacterDiedEvent>(OnDeath);
            context.Events.Subscribe<LoadCompletedEvent>(OnLoaded);
        }
        public void Dispose()
        {
            context.UI?.SetPauseOwner(this, false);
            lighting.SetDarknessFailureOwner(this, false);
            context.Events.Unsubscribe<SceneLoadedEvent>(OnScene);
            context.Events.Unsubscribe<PlayerPositionChangedEvent>(OnPosition);
            context.Events.Unsubscribe<PanelOpenedEvent>(OnPanel);
            context.Events.Unsubscribe<InteractionFocusChangedEvent>(OnFocus);
            context.Events.Unsubscribe<RegionLightsStateChangedEvent>(OnRegion);
            context.Events.Unsubscribe<InventoryChangedEvent>(OnInventory);
            context.Events.Unsubscribe<NoteUnlockedEvent>(OnNote);
            context.Events.Unsubscribe<GuideStepCompletedEvent>(OnPage);
            context.Events.Unsubscribe<MonsterStateChangedEvent>(OnMonsterState);
            context.Events.Unsubscribe<DodgeRequestedEvent>(OnDodge);
            context.Events.Unsubscribe<MonsterDiedEvent>(OnMonsterDied);
            context.Events.Unsubscribe<MonsterDamagedEvent>(OnMonsterDamaged);
            context.Events.Unsubscribe<CharacterDiedEvent>(OnDeath);
            context.Events.Unsubscribe<LoadCompletedEvent>(OnLoaded);
        }
        private void OnScene(SceneLoadedEvent evt)
        {
            var previous = scene;
            scene = evt.SceneId;
            playerPosition = GameRoot.Instance.GetSystem<MemorialArchive.Gameplay.Character.Logic.CharacterSystem>().Data.position;
            hasPosition = true;
            entryDelay = 1;
            sceneReadyFrame = Time.frameCount + 3;
            lighting.SetDarknessFailureOwner(this, OwnsDarknessFailure);
            context.UI?.SetPauseOwner(this, false);
            if (previous == Config?.treatmentScene && scene == Config.encounterScene && progress.keyTakenInTreatment && progress.combatPhase == 0)
            {
                progress.combatPhase = 1;
                narrative.Queue("guide_encounter");
            }
            ResumeCombat();
        }
        private void OnLoaded(LoadCompletedEvent evt)
        {
            if (lighting.IsRegionLit(Config.frontHallRegion))
            {
                guide.Record("light_activated");
                progress.obstacleCleared = true;
            }
            if (narrative.GetCollectedNotes(true).Count > 0)
            {
                progress.mapUnlocked = true;
                guide.Record("map_unlocked");
            }
            ResumeCombat();
        }
        private void ResumeCombat()
        {
            if (!NeedsEncounter) return;
            if (progress.combatPhase == 2) { if (guide.HasCompleted("dodge")) dodgePending = true; else guide.Request("dodge"); }
            if (progress.combatPhase == 3) context.UI?.SetPauseOwner(this, true);
        }
        private void OnPosition(PlayerPositionChangedEvent evt) { playerPosition = evt.Position; hasPosition = true; }
        private void OnPanel(PanelOpenedEvent evt) { if (evt.PanelId == PanelId.Inventory) guide.Request("inventory"); }
        private void OnFocus(InteractionFocusChangedEvent evt)
        {
            if (evt.HasFocus && evt.InteractionType == MemorialArchive.Gameplay.Interaction.Data.InteractionType.LightSource && lighting.IsSpecialLight(evt.InteractionId)) guide.Request("light");
        }
        private void OnRegion(RegionLightsStateChangedEvent evt)
        {
            if (!evt.IsLit || evt.RegionId != Config?.frontHallRegion) return;
            guide.Record("light_activated");
            lighting.SetDarknessFailureOwner(this, false);
            progress.darkSeconds = 0;
            narrative.Queue("guide_lamp_diary");
        }
        private void OnInventory(InventoryChangedEvent evt)
        {
            if (scene == Config?.treatmentScene && inventory.PlayerInventory.playerItems.Exists(p => p.item?.itemId == Config.keyItemId))
                progress.keyTakenInTreatment = true;
        }
        private void OnNote(NoteUnlockedEvent evt)
        {
            if (narrative.Content.FindNote(evt.NoteId)?.isDiary != true) return;
            if (evt.NoteId == Config.diaryInteraction.NoteId) progress.diaryCollected = true;
            progress.mapUnlocked = true;
            guide.Record("map_unlocked");
            guide.Request("systems");
        }
        private void OnPage(GuideStepCompletedEvent evt)
        {
            if (evt.StepId == "dodge" && progress.combatPhase == 2) dodgePending = true;
            if (evt.StepId == "equip" && progress.combatPhase == 3) context.UI?.Open(PanelId.Inventory);
        }
        private void OnMonsterState(MonsterStateChangedEvent evt)
        {
            if (evt.MonsterInstanceId != "guide_melee" || evt.State != MonsterActionState.Attacking || progress.combatPhase != 1) return;
            progress.combatPhase = 2;
            context.UI?.SetPauseOwner(this, true);
            guide.Request("dodge");
        }
        private void OnDodge(DodgeRequestedEvent evt)
        {
            if (progress.combatPhase != 2) return;
            progress.combatPhase = 3;
            dodgePending = false;
            // Allow the requested motion to finish before freezing the equipment lesson.
            equipmentDelay = evt.DurationSeconds + 0.1f;
        }
        private float equipmentDelay;
        private bool HasSelectedWeapon()
        {
            var item = inventory.GetSelectedShortcutPlacement()?.item;
            return item != null && context.Configs.GetItem(item.itemId)?.Category == ItemCategory.Weapon;
        }
        private void OnMonsterDamaged(MonsterDamagedEvent evt)
        { if (evt.MonsterInstanceId == "guide_melee") progress.encounterHealth = Mathf.CeilToInt(evt.RemainingHealth); }
        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            if (evt.MonsterInstanceId != "guide_melee") return;
            progress.combatPhase = 5;
            context.UI?.SetPauseOwner(this, false);
            guide.Record("combat_finished");
            narrative.Queue("guide_victory");
        }
        private void OnDeath(CharacterDiedEvent evt)
        {
            context.UI?.SetPauseOwner(this, false);
            if (NeedsEncounter) { narrative.Queue("guide_combat_defeat"); deathPending = true; }
        }
        public bool TryClearObstacle()
        {
            if (!NeedsObstacle || !lighting.IsLanternLit) return false;
            progress.obstacleCleared = true;
            guide.Record("obstacle_cleared");
            return true;
        }
        public Vector2 ConstrainPosition(Vector2 position)
        {
            if (NeedsObstacle && Config != null)
                position.x = Mathf.Min(position.x, Config.frontHallLeft + Config.equipmentGatePixels / 100 - .6f);
            return position;
        }
        public bool TryInteract()
        {
            if (Config == null || scene != "FrontHall") return false;
            var pixels = (playerPosition.x - Config.frontHallLeft) * 100;
            if (NeedsObstacle && Mathf.Abs(pixels - Config.equipmentGatePixels) <= Config.obstacleInteractionWidth * 50)
            {
                if (!TryClearObstacle()) narrative.Queue("guide_need_lantern");
                return true;
            }
            return false;
        }
        public void Tick(float dt)
        {
            if (Config == null || !HasPosition) return;
            if (scene == "Floor_1F" && progress.mapUnlocked && playerPosition.x > 68 && playerPosition.x < 76) narrative.Queue("guide_stairs");
            if (deathPending)
            {
                // Death normally opens Load immediately. Let the authored defeat reflection precede it.
                if (!narrative.HasPlayed("guide_combat_defeat")) context.UI?.Close(PanelId.Load);
                else { deathPending = false; context.UI?.Open(PanelId.Load); }
            }
            if (scene == "FrontHall") TickFrontHall(dt);
            if (!NeedsEncounter) return;
            if (dodgePending)
            {
                context.UI?.SetPauseOwner(this, false);
                context.Events.Publish(new DodgePressedEvent());
                // If stamina/cooldown prevents dodging, retry on subsequent ticks; completion requires DodgeRequested.
            }
            if (progress.combatPhase == 3)
            {
                equipmentDelay = Mathf.Max(0, equipmentDelay - dt);
                if (equipmentDelay > 0) return;
                context.UI?.SetPauseOwner(this, true);
                if (!HasSelectedWeapon())
                {
                    guide.Request("equip");
                    if (guide.HasCompleted("equip") && guide.Current == null && !context.UI.IsOpen(PanelId.Inventory)) context.UI.Open(PanelId.Inventory);
                    return;
                }
                context.UI?.Close(PanelId.Inventory);
                guide.Request("combat");
                if (guide.HasCompleted("combat")) { progress.combatPhase = 4; context.UI?.SetPauseOwner(this, false); }
            }
        }
        private void TickFrontHall(float dt)
        {
            if (context.UI.IsGameplayInputBlocked || guide.Current?.pause == true) return;
            entryDelay -= dt;
            if (entryDelay <= 0) guide.Request("movement");
            if (!OwnsDarknessFailure) return;
            var pixels = (playerPosition.x - Config.frontHallLeft) * 100;
            if (NeedsObstacle && pixels >= Config.equipmentGatePixels - 140)
                narrative.Queue(lighting.IsLanternLit ? "guide_obstacle" : "guide_need_lantern");
            for (var i = 1; i <= 3; i++)
                if (pixels >= Config.specialLampPixels + Config.warningIntervalPixels * i)
                {
                    narrative.Queue("guide_dark_warning_" + i);
                    lighting.LimitSelectedLanternFuel(i == 1 ? 39 : i == 2 ? 9 : 0);
                }
            if (pixels >= Config.specialLampPixels && !lighting.IsPlayerInLight() && !lighting.IsLanternLit)
            {
                progress.darkSeconds += dt;
                if (progress.darkSeconds >= Config.darknessGraceSeconds)
                {
                    narrative.Queue("guide_dark_ending");
                    if (narrative.HasPlayed("guide_dark_ending"))
                        context.Events.Publish(new DamageRequestedEvent(new DamageRequest(0, "guide_darkness", CombatTargetIds.Player, 0, DamageType.Physical, 10000, playerPosition)));
                }
            }
            else progress.darkSeconds = 0;
        }
        public void ResetForNewGame() { lighting.SetDarknessFailureOwner(this, false); progress = new Progress(); deathPending = dodgePending = false; context.UI?.SetPauseOwner(this, false); }
        public object CaptureSaveData() => progress;
        public void RestoreSaveData(string json)
        {
            progress = string.IsNullOrEmpty(json) ? new Progress() : JsonUtility.FromJson<Progress>(json) ?? new Progress();
            deathPending = dodgePending = false;
            context.UI?.SetPauseOwner(this, false);
        }
    }
}
