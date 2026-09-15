using System.Collections;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Guide.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Guide.View
{
    /// <summary>
    /// 笔记和地图按钮在灯具、笔记地图教程都显示过后解锁。
    /// </summary>
    public sealed class GuideHudVisibility : MonoBehaviour
    {
        [SerializeField] private GameObject[] hiddenDuringGuide;

        private bool subscribed;

        private void OnEnable()
        {
            subscribed = false;
            Apply(false);
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                GameRoot.Instance?.Context?.Events?.Unsubscribe<GuideSequenceActiveChangedEvent>(HandleSequenceActiveChanged);
                GameRoot.Instance?.Context?.Events?.Unsubscribe<GuideStepStartedEvent>(HandleStepStarted);
                subscribed = false;
            }
        }

        private IEnumerator Start()
        {
            // GameRoot 可能晚于本物体就绪，轮询补订阅并同步初始状态（TrySubscribe 内部会判空）。
            while (!subscribed)
            {
                TrySubscribe();
                yield return null;
            }
        }

        private void TrySubscribe()
        {
            var events = GameRoot.Instance?.Context?.Events;
            if (events == null)
            {
                return;
            }

            events.Subscribe<GuideSequenceActiveChangedEvent>(HandleSequenceActiveChanged);
            events.Subscribe<GuideStepStartedEvent>(HandleStepStarted);
            subscribed = true;
            Refresh();
        }

        private void HandleSequenceActiveChanged(GuideSequenceActiveChangedEvent evt) => Refresh();
        private void HandleStepStarted(GuideStepStartedEvent evt) => Refresh();
        private void Refresh() => Apply(GameRoot.Instance?.GetSystem<GuideSystem>()?.DiaryMapButtonsVisible ?? false);

        private void Apply(bool visible)
        {
            if (hiddenDuringGuide == null)
            {
                return;
            }

            foreach (var target in hiddenDuringGuide)
            {
                if (target != null)
                {
                    target.SetActive(visible);
                }
            }
        }
    }
}
