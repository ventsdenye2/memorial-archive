using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Interaction.View;
using MemorialArchive.Gameplay.Inventory.View;
using UnityEngine;

namespace MemorialArchive.Gameplay.Stage1
{
    public sealed class Stage1DemoSceneValidator : MonoBehaviour
    {
        private static readonly string[] RequiredInteractionIds =
        {
            Stage1Ids.ContainerPoint01,
            Stage1Ids.ContainerPoint02,
            Stage1Ids.SavePoint01,
            Stage1Ids.InspectPoint01,
            Stage1Ids.EntrancePoint01
        };

        private static readonly int[] RequiredItemIds = { 1002, 1003, 1012, 1020, 1006, 1024 };

        [SerializeField] private GameObject blackScreenStoryRoot;
        [SerializeField] private GameObject stage1DemoRoot;
        [SerializeField] private ScenePanelRegistry panelRegistry;

        private void Start()
        {
            ValidateNow();
        }

        [ContextMenu("Validate Stage 1 Scene")]
        public bool ValidateNow()
        {
            var valid = true;
            valid &= Require(blackScreenStoryRoot != null, "BlackScreenStoryRoot reference is missing.");
            valid &= Require(stage1DemoRoot != null, "Stage1DemoRoot reference is missing.");
            valid &= Require(panelRegistry != null, "ScenePanelRegistry reference is missing.");
            valid &= Require(GameRoot.Instance != null, "A GameRoot must exist when the scene starts.");
            valid &= Require(FindObjectsOfType<MemorialArchive.Gameplay.Character.View.PlayerMotor>(true).Length == 1, "Exactly one PlayerMotor is required.");
            valid &= Require(FindObjectsOfType<MemorialArchive.Gameplay.Camera.View.CameraFollowView>(true).Length == 1, "Exactly one CameraFollowView is required.");

            var foundIds = new HashSet<string>();
            foreach (var point in FindObjectsOfType<InteractionPointView>(true))
            {
                if (!string.IsNullOrEmpty(point.InteractionId))
                {
                    foundIds.Add(point.InteractionId);
                    var config = GameRoot.Instance?.Context?.Configs?.GetInteraction(point.InteractionId);
                    valid &= Require(config != null, $"InteractionConfig is missing for {point.InteractionId}");
                    if (config != null)
                    {
                        valid &= Require(config.InteractionType == point.InteractionType,
                            $"Interaction type mismatch for {point.InteractionId}: scene={point.InteractionType}, config={config.InteractionType}");
                    }
                }
            }

            foreach (var requiredId in RequiredInteractionIds)
            {
                valid &= Require(foundIds.Contains(requiredId), $"Required interaction ID is missing: {requiredId}");
            }

            var foundSpawn = false;
            foreach (var point in FindObjectsOfType<Stage1NamedPoint>(true))
            {
                foundSpawn |= point.PointId == Stage1Ids.SpawnPoint;
            }

            valid &= Require(foundSpawn, $"Required spawn point is missing: {Stage1Ids.SpawnPoint}");
            foreach (var itemId in RequiredItemIds)
            {
                valid &= Require(GameRoot.Instance?.Context?.Configs?.GetItem(itemId) != null,
                    $"ItemConfig is missing for item ID {itemId}");
            }

            var containerIds = new HashSet<string>();
            foreach (var seed in FindObjectsOfType<SceneContainerSeedView>(true))
            {
                if (!string.IsNullOrEmpty(seed.ContainerId))
                {
                    containerIds.Add(seed.ContainerId);
                }
            }

            valid &= Require(containerIds.Contains(Stage1Ids.DemoContainer01), $"Missing container seed: {Stage1Ids.DemoContainer01}");
            valid &= Require(containerIds.Contains(Stage1Ids.DemoContainer02), $"Missing container seed: {Stage1Ids.DemoContainer02}");
            valid &= Require(HasSeedSet(Stage1Ids.DemoContainer01, 1002, 1003, 1012),
                $"Container seed {Stage1Ids.DemoContainer01} must contain 1002, 1003 and 1012.");
            valid &= Require(HasSeedSet(Stage1Ids.DemoContainer02, 1020, 1006, 1024),
                $"Container seed {Stage1Ids.DemoContainer02} must contain 1020, 1006 and 1024.");

            if (valid)
            {
                Debug.Log("Stage1DemoSceneValidator: scene contract passed.", this);
            }

            return valid;
        }

        private static bool HasSeedSet(string containerId, params int[] expectedItemIds)
        {
            var itemIds = new HashSet<int>();
            foreach (var seedView in FindObjectsOfType<SceneContainerSeedView>(true))
            {
                if (seedView.ContainerId != containerId || seedView.InitialItems == null)
                {
                    continue;
                }

                foreach (var seed in seedView.InitialItems)
                {
                    if (seed != null)
                    {
                        itemIds.Add(seed.itemId);
                    }
                }
            }

            foreach (var expectedItemId in expectedItemIds)
            {
                if (!itemIds.Contains(expectedItemId))
                {
                    return false;
                }
            }

            return true;
        }

        private bool Require(bool condition, string message)
        {
            if (!condition)
            {
                Debug.LogError($"Stage1DemoSceneValidator: {message}", this);
            }

            return condition;
        }
    }
}
