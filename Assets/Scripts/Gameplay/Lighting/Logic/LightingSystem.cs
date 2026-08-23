using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.Save;
using MemorialArchive.Gameplay.Combat.Data;
using MemorialArchive.Gameplay.Inventory.Data;
using MemorialArchive.Gameplay.Inventory.Logic;
using MemorialArchive.Gameplay.Item.Config;
using MemorialArchive.Gameplay.Lighting.Config;
using MemorialArchive.Gameplay.Lighting.Data;
using UnityEngine;

namespace MemorialArchive.Gameplay.Lighting.Logic
{
    /// <summary>
    /// 光照系统：区域灯光状态、临时灯计时、手提灯燃料状态机与黑暗判定。
    /// 规则全部在本系统内，View 只注册信息与订阅事件。
    /// 游戏失败规则：燃料耗尽（或未装备手提灯）且处于全黑时，经标准伤害入口
    /// 造成致死伤害，走既有死亡流程（死亡动画 + 读档面板）。
    /// </summary>
    public sealed class LightingSystem : IGameSystem, ITickableSystem, ISaveModule, INewGameResettable
    {
        private static readonly InventoryContainerKind[] PlayerItemKinds =
        {
            InventoryContainerKind.Offhand,
            InventoryContainerKind.ShortcutBar,
            InventoryContainerKind.Backpack
        };

        private readonly InventorySystem inventory;
        private readonly Dictionary<string, LightViewRegistration> lightViews = new Dictionary<string, LightViewRegistration>();
        private readonly Dictionary<string, bool> regionLitStates = new Dictionary<string, bool>();
        private readonly Dictionary<string, float> tempLightRemaining = new Dictionary<string, float>();
        private readonly Dictionary<string, float> lanternFuelByInstance = new Dictionary<string, float>();

        private GameContext context;
        private LightingGlobalConfig config;
        private string activeSceneName;

        private bool isLanternEquipped;
        private string equippedLanternInstanceId;
        private bool isLanternLit;
        private float fuelPublishTimer;
        private float failureCooldownRemaining;
        private Vector2 lastPlayerPosition;

        public string ModuleKey => "lighting";

        public LightingSystem(InventorySystem inventory)
        {
            this.inventory = inventory;
        }

        public bool IsLanternEquipped => isLanternEquipped;
        public bool IsLanternLit => isLanternLit;
        public float LanternFuelRemaining => GetEquippedLanternFuel();
        public float LanternFuelTotal => config != null ? config.LanternTotalFuelSeconds : 60f;

        public void Initialize(GameContext context)
        {
            this.context = context;
            config = context.Configs.GetLightingGlobal();
            if (config == null)
            {
                Debug.LogWarning("[LightingSystem] LightingGlobalConfig 未注册到 GameConfigDatabase，使用默认参数。");
                config = ScriptableObject.CreateInstance<LightingGlobalConfig>();
            }

            activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            context.Events.Subscribe<LightSourceInteractRequestedEvent>(HandleLightSourceInteractRequested);
            context.Events.Subscribe<LanternTogglePressedEvent>(HandleLanternTogglePressed);
            context.Events.Subscribe<CharacterEquipmentChangedEvent>(HandleEquipmentChanged);
            context.Events.Subscribe<SceneLoadedEvent>(HandleSceneLoaded);
            context.Events.Subscribe<LoadCompletedEvent>(HandleLoadCompleted);
            context.Events.Subscribe<PlayerPositionChangedEvent>(HandlePlayerPositionChanged);

            RequeryLanternEquipment();
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Events.Unsubscribe<LightSourceInteractRequestedEvent>(HandleLightSourceInteractRequested);
                context.Events.Unsubscribe<LanternTogglePressedEvent>(HandleLanternTogglePressed);
                context.Events.Unsubscribe<CharacterEquipmentChangedEvent>(HandleEquipmentChanged);
                context.Events.Unsubscribe<SceneLoadedEvent>(HandleSceneLoaded);
                context.Events.Unsubscribe<LoadCompletedEvent>(HandleLoadCompleted);
                context.Events.Unsubscribe<PlayerPositionChangedEvent>(HandlePlayerPositionChanged);
            }

            context = null;
            config = null;
            lightViews.Clear();
            regionLitStates.Clear();
            tempLightRemaining.Clear();
            lanternFuelByInstance.Clear();
            activeSceneName = null;
            isLanternEquipped = false;
            equippedLanternInstanceId = null;
            isLanternLit = false;
        }

        public void ResetForNewGame()
        {
            regionLitStates.Clear();
            tempLightRemaining.Clear();
            lanternFuelByInstance.Clear();
            isLanternLit = false;
            equippedLanternInstanceId = null;
            failureCooldownRemaining = 0f;
        }

        public void Tick(float deltaTime)
        {
            TickLanternFuel(deltaTime);
            TickTempLights(deltaTime);
            TickDarknessFailure(deltaTime);
        }

        // ---- 公共查询（交互门禁与黑暗层使用） ----

        /// <summary>玩家当前是否有光源：手提灯点亮、所在场景有已点亮区域或有亮着的临时灯。</summary>
        public bool IsPlayerInLight()
        {
            if (!SceneHasLightingViews)
            {
                return true;
            }

            if (isLanternLit && isLanternEquipped)
            {
                return true;
            }

            return IsCurrentSceneLit();
        }

        /// <summary>当前场景是否整体处于点亮状态（无灯光视图的场景视为点亮，不参与黑暗玩法）。</summary>
        public bool IsCurrentSceneLit()
        {
            var hasAnyRegion = false;
            foreach (var view in lightViews.Values)
            {
                if (view.sceneName != activeSceneName)
                {
                    continue;
                }

                hasAnyRegion = true;
                if (!IsRegionLit(view.regionId))
                {
                    return false;
                }
            }

            if (hasAnyRegion)
            {
                return true;
            }

            // 场景没有区域视图，但可能有亮着的临时灯。
            return HasActiveSceneTempLight();
        }

        public bool IsRegionLit(string regionId)
        {
            return !string.IsNullOrEmpty(regionId) &&
                   regionLitStates.TryGetValue(regionId, out var isLit) &&
                   isLit;
        }

        public bool IsLightOn(string lightId)
        {
            if (!string.IsNullOrEmpty(lightId) &&
                tempLightRemaining.TryGetValue(lightId, out var remaining) &&
                remaining > 0f)
            {
                return true;
            }

            return lightViews.TryGetValue(lightId, out var view) && IsRegionLit(view.regionId);
        }

        /// <summary>填充黑暗层需要的活跃光源：点亮中的手提灯 + 当前场景亮着的临时灯。</summary>
        public void CollectActiveLights(List<ActiveLight> results)
        {
            results.Clear();

            if (isLanternLit && isLanternEquipped)
            {
                // 玩家位置事件来自角色脚底原点，抬升到躯干高度让光圈居中。
                var lanternPosition = new Vector2(
                    lastPlayerPosition.x,
                    lastPlayerPosition.y + config.LanternLightYOffset);
                results.Add(new ActiveLight(lanternPosition, GetCurrentLanternRadius()));
            }

            foreach (var pair in tempLightRemaining)
            {
                if (pair.Value <= 0f)
                {
                    continue;
                }

                if (lightViews.TryGetValue(pair.Key, out var view) && view.sceneName == activeSceneName)
                {
                    results.Add(new ActiveLight(view.position, view.radius));
                }
            }
        }

        // ---- View 注册 ----

        public void RegisterLightView(LightViewRegistration registration)
        {
            if (registration == null || string.IsNullOrEmpty(registration.lightId))
            {
                return;
            }

            lightViews[registration.lightId] = registration;
        }

        public void UnregisterLightView(string lightId)
        {
            if (!string.IsNullOrEmpty(lightId))
            {
                lightViews.Remove(lightId);
            }
        }

        // ---- 存档 ----

        public object CaptureSaveData()
        {
            var saveData = new LightingSaveData();
            foreach (var pair in regionLitStates)
            {
                saveData.regions.Add(new RegionSaveData { regionId = pair.Key, isLit = pair.Value });
            }

            foreach (var pair in lanternFuelByInstance)
            {
                saveData.lanternFuel.Add(new LanternFuelSaveData { instanceId = pair.Key, remainingSeconds = pair.Value });
            }

            return saveData;
        }

        public void RestoreSaveData(string json)
        {
            regionLitStates.Clear();
            tempLightRemaining.Clear();
            lanternFuelByInstance.Clear();
            isLanternLit = false;
            equippedLanternInstanceId = null;

            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var saveData = JsonUtility.FromJson<LightingSaveData>(json);
            if (saveData == null)
            {
                return;
            }

            if (saveData.regions != null)
            {
                foreach (var region in saveData.regions)
                {
                    if (region != null && !string.IsNullOrEmpty(region.regionId))
                    {
                        regionLitStates[region.regionId] = region.isLit;
                    }
                }
            }

            if (saveData.lanternFuel != null)
            {
                foreach (var fuel in saveData.lanternFuel)
                {
                    if (fuel != null && !string.IsNullOrEmpty(fuel.instanceId))
                    {
                        lanternFuelByInstance[fuel.instanceId] = Mathf.Max(0f, fuel.remainingSeconds);
                    }
                }
            }

            RequeryLanternEquipment();
        }

        // ---- 内部逻辑 ----

        private bool SceneHasLightingViews
        {
            get
            {
                foreach (var view in lightViews.Values)
                {
                    if (view.sceneName == activeSceneName)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private bool HasActiveSceneTempLight()
        {
            foreach (var pair in tempLightRemaining)
            {
                if (pair.Value > 0f &&
                    lightViews.TryGetValue(pair.Key, out var view) &&
                    view.sceneName == activeSceneName)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleLightSourceInteractRequested(LightSourceInteractRequestedEvent evt)
        {
            if (!isLanternEquipped)
            {
                context.Events.Publish(new LightInteractionFailedEvent(evt.LightId, "需要先装备手提灯"));
                return;
            }

            var lightConfig = context.Configs.GetLightSource(evt.LightId);
            if (lightConfig == null)
            {
                Debug.LogWarning($"[LightingSystem] 找不到灯具配置 LightSourceConfig：{evt.LightId}");
                return;
            }

            if (lightConfig.IsSpecial)
            {
                regionLitStates[lightConfig.RegionId] = true;
                context.Events.Publish(new RegionLightsStateChangedEvent(lightConfig.RegionId, true));
                foreach (var view in lightViews.Values)
                {
                    if (view.regionId == lightConfig.RegionId)
                    {
                        context.Events.Publish(new LightStateChangedEvent(view.lightId, true, 0f));
                    }
                }

                RefuelEquippedLantern();
            }
            else
            {
                tempLightRemaining[evt.LightId] = config.TempLightSeconds;
                context.Events.Publish(new LightStateChangedEvent(evt.LightId, true, config.TempLightSeconds));
            }
        }

        private void HandleLanternTogglePressed(LanternTogglePressedEvent evt)
        {
            if (!isLanternEquipped)
            {
                context.Events.Publish(new LanternToggleFailedEvent("未装备手提灯"));
                return;
            }

            if (isLanternLit)
            {
                SetLanternLit(false);
                return;
            }

            if (GetEquippedLanternFuel() <= 0f)
            {
                context.Events.Publish(new LanternToggleFailedEvent("手提灯燃料已耗尽"));
                return;
            }

            SetLanternLit(true);
        }

        private void HandleEquipmentChanged(CharacterEquipmentChangedEvent evt)
        {
            RequeryLanternEquipment();
        }

        private void HandleSceneLoaded(SceneLoadedEvent evt)
        {
            activeSceneName = evt.SceneId;
            RequeryLanternEquipment();
        }

        private void HandleLoadCompleted(LoadCompletedEvent evt)
        {
            // 读档后手提灯一律为熄灭状态，按 E 重新点亮。
            SetLanternLit(false);
            RequeryLanternEquipment();
        }

        private void HandlePlayerPositionChanged(PlayerPositionChangedEvent evt)
        {
            lastPlayerPosition = evt.Position;
        }

        private void RequeryLanternEquipment()
        {
            string foundInstanceId = null;
            var foundItemId = 0;
            if (inventory != null)
            {
                foreach (var placement in inventory.GetPlayerPlacements(InventoryContainerKind.Offhand))
                {
                    if (placement?.item == null)
                    {
                        continue;
                    }

                    var itemConfig = context?.Configs?.GetItem(placement.item.itemId);
                    if (itemConfig != null && itemConfig.OffhandType == OffhandType.Lantern)
                    {
                        foundInstanceId = placement.item.instanceId;
                        foundItemId = placement.item.itemId;
                        break;
                    }
                }
            }

            var wasEquipped = isLanternEquipped;
            var previousInstanceId = equippedLanternInstanceId;
            isLanternEquipped = foundInstanceId != null;
            equippedLanternInstanceId = foundInstanceId;

            if (foundItemId != 0 && foundInstanceId != null && !lanternFuelByInstance.ContainsKey(foundInstanceId))
            {
                lanternFuelByInstance[foundInstanceId] = config != null ? config.LanternTotalFuelSeconds : 60f;
            }

            if (!isLanternEquipped)
            {
                SetLanternLit(false);
                return;
            }

            // 副手栏只有一格，装备即视为需求中的“选中”；首次装备且有燃料时自动点亮。
            var isNewEquip = !wasEquipped || previousInstanceId != foundInstanceId;
            if (isNewEquip && config != null && config.AutoLightOnEquip && !isLanternLit)
            {
                SetLanternLit(GetEquippedLanternFuel() > 0f);
            }
        }

        private float GetEquippedLanternFuel()
        {
            if (equippedLanternInstanceId == null)
            {
                return 0f;
            }

            return lanternFuelByInstance.TryGetValue(equippedLanternInstanceId, out var fuel) ? fuel : 0f;
        }

        private void RefuelEquippedLantern()
        {
            if (equippedLanternInstanceId == null)
            {
                return;
            }

            lanternFuelByInstance[equippedLanternInstanceId] = config.LanternTotalFuelSeconds;
            PublishLanternFuel();
        }

        private void SetLanternLit(bool lit)
        {
            if (isLanternLit == lit)
            {
                return;
            }

            isLanternLit = lit;
            context?.Events.Publish(new LanternLitChangedEvent(isLanternLit));
            PublishLanternFuel();
        }

        private void TickLanternFuel(float deltaTime)
        {
            if (!isLanternLit || !isLanternEquipped || equippedLanternInstanceId == null)
            {
                return;
            }

            var fuel = GetEquippedLanternFuel();
            var previousStage = CalculateStage(fuel);
            fuel = Mathf.Max(0f, fuel - deltaTime);
            lanternFuelByInstance[equippedLanternInstanceId] = fuel;

            if (fuel <= 0f)
            {
                SetLanternLit(false);
                return;
            }

            fuelPublishTimer += deltaTime;
            if (CalculateStage(fuel) != previousStage || fuelPublishTimer >= 1f)
            {
                fuelPublishTimer = 0f;
                PublishLanternFuel();
            }
        }

        private void TickTempLights(float deltaTime)
        {
            if (tempLightRemaining.Count == 0)
            {
                return;
            }

            var expired = null as List<string>;
            foreach (var pair in tempLightRemaining)
            {
                var remaining = pair.Value - deltaTime;
                tempLightRemaining[pair.Key] = remaining;
                if (remaining <= 0f)
                {
                    (expired ?? (expired = new List<string>())).Add(pair.Key);
                }
            }

            if (expired == null)
            {
                return;
            }

            foreach (var lightId in expired)
            {
                tempLightRemaining.Remove(lightId);
                context.Events.Publish(new LightStateChangedEvent(lightId, false, 0f));
            }
        }

        private void TickDarknessFailure(float deltaTime)
        {
            failureCooldownRemaining = Mathf.Max(0f, failureCooldownRemaining - deltaTime);

            // 游戏失败条件：玩家处于全黑，且没有任何可点亮的手提灯
            // （副手/快捷栏/背包均无有燃料的灯）。灯在背包里未装备属于
            // 可恢复状态（引导开局就是这种情况），不判失败。
            if (HasAnyLightableLantern() || IsPlayerInLight())
            {
                return;
            }

            if (failureCooldownRemaining > 0f)
            {
                return;
            }

            failureCooldownRemaining = config.FailureRetrySeconds;
            context.Events.Publish(new DamageRequestedEvent(new DamageRequest(
                0,
                "lighting_failure",
                CombatTargetIds.Player,
                0,
                DamageType.Physical,
                config.DarknessFailureDamage,
                lastPlayerPosition)));
        }

        private bool HasAnyLightableLantern()
        {
            if (inventory == null)
            {
                return false;
            }

            foreach (var kind in PlayerItemKinds)
            {
                foreach (var placement in inventory.GetPlayerPlacements(kind))
                {
                    if (placement?.item == null)
                    {
                        continue;
                    }

                    var itemConfig = context?.Configs?.GetItem(placement.item.itemId);
                    if (itemConfig == null || itemConfig.OffhandType != OffhandType.Lantern)
                    {
                        continue;
                    }

                    // 从未装备过的手提灯没有燃料记录，视为满燃料。
                    var fuel = lanternFuelByInstance.TryGetValue(placement.item.instanceId, out var recorded)
                        ? recorded
                        : config.LanternTotalFuelSeconds;
                    if (fuel > 0f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private LanternStage GetCurrentStage()
        {
            return CalculateStage(GetEquippedLanternFuel());
        }

        private LanternStage CalculateStage(float fuel)
        {
            if (fuel <= 0f)
            {
                return LanternStage.Off;
            }

            if (fuel > config.LanternStrongThresholdSeconds)
            {
                return LanternStage.Strong;
            }

            return fuel > config.LanternWeakThresholdSeconds ? LanternStage.Normal : LanternStage.Weak;
        }

        private float GetCurrentLanternRadius()
        {
            switch (GetCurrentStage())
            {
                case LanternStage.Strong:
                    return config.LanternStrongRadius;
                case LanternStage.Normal:
                    return config.LanternNormalRadius;
                case LanternStage.Weak:
                    return config.LanternWeakRadius;
                default:
                    return 0f;
            }
        }

        private void PublishLanternFuel()
        {
            context?.Events.Publish(new LanternFuelChangedEvent(
                GetEquippedLanternFuel(),
                config != null ? config.LanternTotalFuelSeconds : 60f,
                GetCurrentStage()));
        }
    }
}
