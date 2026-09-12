using MemorialArchive.Framework.Core;
using MemorialArchive.Gameplay.Story.Logic;
using Spine.Unity;
using UnityEngine;

namespace MemorialArchive.Gameplay.Story.View
{
    public sealed class CecilView : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation skeleton;
        private NarrativeSystem narrative;
        private float opacity = 1f;
        private Collider2D interaction;
        private void Start()
        {
            narrative = GameRoot.Instance?.GetSystem<NarrativeSystem>();
            interaction = GetComponent<Collider2D>();
            if (narrative?.HasPlayed("cecil_conversation") == true) opacity = 0f;
            if (skeleton != null) skeleton.Initialize(false);
        }
        private void Update()
        {
            var fade = narrative != null && (narrative.HasPlayed("cecil_conversation") ||
                narrative.Current?.id == "cecil_conversation" && narrative.CurrentLine.fadeCecil);
            opacity = Mathf.MoveTowards(opacity, fade ? 0f : 1f, Time.unscaledDeltaTime * 1.5f);
            if (skeleton != null && Time.timeScale == 0f) skeleton.Update(Time.unscaledDeltaTime);
            if (skeleton?.Skeleton != null) skeleton.Skeleton.A = opacity;
            if (interaction != null) interaction.enabled = opacity > 0f && !fade;
        }
    }
}
