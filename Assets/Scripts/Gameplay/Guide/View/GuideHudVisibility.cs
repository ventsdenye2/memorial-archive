using System.Collections;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Gameplay.Guide.Logic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Guide.View
{
    /// <summary>
    /// 引导序列进行期间隐藏 HUD 上不该出现的图标（需求 2.1 第 8 点：该阶段笔记和地图图标不出现）。
    /// 只订阅 GuideSequenceActiveChangedEvent 切换显隐，不改 HUD 面板本身的逻辑。
    /// </summary>
    public sealed class GuideHudVisibility : MonoBehaviour
    {
        [SerializeField] private GameObject[] hiddenDuringGuide;

        private bool subscribed;

        private void OnEnable()
        {
            subscribed = false;
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                GameRoot.Instance?.Context?.Events?.Unsubscribe<GuideSequenceActiveChangedEvent>(HandleSequenceActiveChanged);
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
            subscribed = true;
            Apply(GameRoot.Instance?.GetSystem<GuideSystem>()?.SequenceActive ?? false);
        }

        private void HandleSequenceActiveChanged(GuideSequenceActiveChangedEvent evt) => Apply(evt.IsActive);

        private void Apply(bool guideActive)
        {
            if (hiddenDuringGuide == null)
            {
                return;
            }

            foreach (var target in hiddenDuringGuide)
            {
                if (target != null)
                {
                    target.SetActive(!guideActive);
                }
            }
        }
    }
}
