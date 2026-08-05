using MemorialArchive.Framework.UI;
using UnityEditor;
using UnityEngine;

namespace MemorialArchive.Editor
{
    public static class InventoryPanelPrefabBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/UI/InventoryPanel.prefab";

        [MenuItem("Tools/Memorial Archive/Rebuild Inventory Panel Prefab")]
        public static void RebuildPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var panel = root.GetComponent<InventoryPanel>();
                if (panel == null)
                {
                    throw new System.InvalidOperationException("InventoryPanel component is missing from its prefab.");
                }

                panel.RebuildPrefabLayoutForEditor();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("InventoryPanel prefab rebuilt with editable static UI children.");
        }
    }
}
