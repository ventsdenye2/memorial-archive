using System.Collections.Generic;
using MemorialArchive.Framework.Core;
using UnityEngine;

namespace MemorialArchive.Framework.UI
{
    public sealed class ScenePanelRegistry : MonoBehaviour
    {
        [SerializeField] private List<BasePanel> panels = new List<BasePanel>();

        public IReadOnlyList<BasePanel> Panels => panels;

        private void Start()
        {
            Register();
        }

        private void OnDestroy()
        {
            var ui = GameRoot.Instance?.Context?.UI;
            if (ui != null)
            {
                ui.UnregisterScenePanels(panels);
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
        }
    }
}
