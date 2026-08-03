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
            while (!registered)
            {
                if (GameRoot.Instance?.Context?.UI != null)
                {
                    Register();
                    yield break;
                }

                yield return null;
            }
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
