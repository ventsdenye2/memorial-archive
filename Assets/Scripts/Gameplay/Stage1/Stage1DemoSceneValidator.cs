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

            if (valid)
            {
                Debug.Log("Stage1DemoSceneValidator: scene contract passed.", this);
            }

            return valid;
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
