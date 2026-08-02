using System.Collections;
using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using UnityEngine;

namespace MemorialArchive.Framework.UI
{
    public sealed class ScenePanelRegistry : MonoBehaviour
    {
        [SerializeField] private List<BasePanel> panels = new List<BasePanel>();
        private bool registered;

        public IReadOnlyList<BasePanel> Panels => panels;

        private void Awake()
        {
            TryRegister();
        }

        private IEnumerator Start()
        {
            if (registered)
            {
                yield break;
            }

            // Gameplay scenes normally inherit the persistent GameRoot from the
            // main menu. Waiting also keeps direct scene loading failure explicit.
            for (var attempts = 0; attempts < 60; attempts++)
            {
                if (GameRoot.Instance?.Context?.UI != null)
                {
                    Register();
                    yield break;
                }

                yield return null;
            }

            Debug.LogError($"ScenePanelRegistry on {name} cannot find the persistent UIManager.");
        }

        private void OnDestroy()
        {
            if (registered)
            {
                GameRoot.Instance?.Context?.UI?.UnregisterScenePanels(panels);
            }
        }

        public void Register()
        {
            var ui = GameRoot.Instance?.Context?.UI;
            if (ui == null)
            {
                Debug.LogError($"ScenePanelRegistry on {name} cannot find the persistent UIManager.");
                return;
            }

            ui.RegisterScenePanels(panels);
            registered = true;
        }

        private void TryRegister()
        {
            if (!registered && GameRoot.Instance?.Context?.UI != null)
            {
                Register();
            }
        }
    }
}
