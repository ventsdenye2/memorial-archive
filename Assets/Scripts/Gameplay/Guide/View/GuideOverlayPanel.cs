using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using MemorialArchive.Framework.Event;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Guide.Config;
using MemorialArchive.Gameplay.Guide.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Gameplay.Guide.View
{
    /// <summary>
    /// 通用引导覆盖层。只根据 GuideStepStarted/Hidden 事件显示或收起提示条，
    /// 不自行推进步骤（分工约束：完成条件判定归 GuideSystem）。
    /// 作为非模态覆盖层挂在持久 Canvas 上，不暂停游戏、不阻塞输入。
    /// </summary>
    public sealed class GuideOverlayPanel : BasePanel
    {
        [SerializeField] private GameObject entryPrefab;
        [SerializeField] private RectTransform entryContainer;
        [SerializeField] private int maxVisibleEntries = 4;

        private readonly Dictionary<string, GuideEntryView> visibleEntries = new Dictionary<string, GuideEntryView>();

        protected override void Awake()
        {
            base.Awake();
            if (entryPrefab != null)
            {
                entryPrefab.SetActive(false);
            }
        }

        private void OnEnable()
        {
            var events = GameRoot.Instance?.Context?.Events;
            if (events != null)
            {
                events.Subscribe<GuideStepStartedEvent>(HandleStepStarted);
                events.Subscribe<GuideStepHiddenEvent>(HandleStepHidden);
            }
        }

        private void OnDisable()
        {
            var events = GameRoot.Instance?.Context?.Events;
            if (events != null)
            {
                events.Unsubscribe<GuideStepStartedEvent>(HandleStepStarted);
                events.Unsubscribe<GuideStepHiddenEvent>(HandleStepHidden);
            }
        }

        private void HandleStepStarted(GuideStepStartedEvent evt)
        {
            if (visibleEntries.ContainsKey(evt.StepId))
            {
                return;
            }

            var config = LookupStepConfig(evt.StepId);
            if (config == null)
            {
                return;
            }

            var entry = CreateEntry();
            if (entry == null)
            {
                return;
            }

            entry.Bind(config);
            visibleEntries[evt.StepId] = entry;
            EnforceMaxVisible();
        }

        private void HandleStepHidden(GuideStepHiddenEvent evt)
        {
            if (visibleEntries.TryGetValue(evt.StepId, out var entry))
            {
                entry.Release();
                visibleEntries.Remove(evt.StepId);
            }
        }

        private GuideStepConfig LookupStepConfig(string stepId)
        {
            // 步骤配置内嵌于序列配置中；通过 GuideSystem 暴露的运行时序列访问。
            var guideSystem = GameRoot.Instance?.GetSystem<GuideSystem>();
            return guideSystem?.FindStepConfig(stepId);
        }

        private GuideEntryView CreateEntry()
        {
            if (entryPrefab == null || entryContainer == null)
            {
                return null;
            }

            var instance = Instantiate(entryPrefab, entryContainer, false);
            instance.SetActive(true);
            return new GuideEntryView(instance);
        }

        private void EnforceMaxVisible()
        {
            // 超出上限时移除最早的条目（按 Dictionary 顺序近似 FIFO，足够引导场景使用）。
            while (visibleEntries.Count > maxVisibleEntries)
            {
                string oldest = null;
                foreach (var pair in visibleEntries)
                {
                    oldest = pair.Key;
                    break;
                }

                if (oldest == null)
                {
                    break;
                }

                visibleEntries[oldest].Release();
                visibleEntries.Remove(oldest);
            }
        }

        /// <summary>单个引导提示条目的表现层，持有其 GameObject 引用。</summary>
        private sealed class GuideEntryView
        {
            private readonly GameObject root;

            public GuideEntryView(GameObject root)
            {
                this.root = root;
            }

            public void Bind(GuideStepConfig config)
            {
                if (root == null || config == null)
                {
                    return;
                }

                var title = FindComponent<Text>(root, "Title");
                if (title != null)
                {
                    title.text = config.Title ?? string.Empty;
                }

                var description = FindComponent<Text>(root, "Description");
                if (description != null)
                {
                    description.text = config.Description ?? string.Empty;
                }

                var icon = FindComponent<Image>(root, "Icon");
                if (icon != null)
                {
                    if (config.Sprite != null)
                    {
                        icon.sprite = config.Sprite;
                        icon.enabled = true;
                    }
                    else
                    {
                        icon.enabled = false;
                    }
                }
            }

            public void Release()
            {
                if (root != null)
                {
                    Destroy(root);
                }
            }

            private static T FindComponent<T>(GameObject root, string childName) where T : Component
            {
                if (root == null || string.IsNullOrEmpty(childName))
                {
                    return null;
                }

                var transform = root.transform.Find(childName);
                return transform != null ? transform.GetComponent<T>() : root.GetComponentInChildren<T>();
            }
        }
    }
}
