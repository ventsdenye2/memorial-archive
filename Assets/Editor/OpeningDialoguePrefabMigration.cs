#if UNITY_EDITOR
using MemorialArchive.Gameplay.Dialogue.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.EditorTools
{
    /// <summary>Idempotently applies the opening dialogue layout contract to its prefab.</summary>
    public static class OpeningDialoguePrefabMigration
    {
        private const string PrefabPath = "Assets/Prefabs/UI/OpeningDialoguePanel.prefab";

        [MenuItem("Memorial Archive/Migrations/Apply Opening Dialogue Presentation")]
        public static void Apply()
        {
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefab == null) throw new System.IO.FileNotFoundException(PrefabPath);
            try
            {
                var panel = prefab.transform.Find("DialoguePanel");
                var text = panel.Find("DialogueText").GetComponent<Text>();
                var frame = panel.Find("DialogueFrame").GetComponent<RectTransform>();
                var hint = panel.Find("Hint").GetComponent<RectTransform>();
                var name = panel.Find("SpeakerName").GetComponent<RectTransform>();
                var view = panel.GetComponent<DialoguePresentationView>();
                var specialNode = panel.Find("SpecialText");
                if (specialNode == null)
                {
                    var go = new GameObject("SpecialText", typeof(RectTransform), typeof(Text));
                    go.transform.SetParent(panel, false);
                    specialNode = go.transform;
                }
                var specialText = specialNode.GetComponent<Text>();
                var sourceRect = text.rectTransform;
                sourceRect.anchoredPosition = new Vector2(0f, 140f);
                sourceRect.sizeDelta = new Vector2(1600f, 154f);
                var specialRect = specialText.rectTransform;
                specialRect.anchorMin = sourceRect.anchorMin;
                specialRect.anchorMax = sourceRect.anchorMax;
                specialRect.pivot = sourceRect.pivot;
                specialRect.anchoredPosition = sourceRect.anchoredPosition;
                specialRect.sizeDelta = sourceRect.sizeDelta;
                specialText.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/FZZJ-LZXTFSJW.TTF");
                specialText.fontSize = 32;
                specialText.color = Color.white;
                specialText.alignment = TextAnchor.UpperLeft;
                specialText.verticalOverflow = VerticalWrapMode.Overflow;
                specialText.supportRichText = true;
                specialText.raycastTarget = false;
                var specialTracking = specialText.GetComponent<DialogueTracking>();
                if (specialTracking == null) specialTracking = specialText.gameObject.AddComponent<DialogueTracking>();
                specialTracking.Tracking = 12f;

                text.supportRichText = true;
                text.resizeTextForBestFit = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                if (text.GetComponent<DialogueTracking>() == null)
                    text.gameObject.AddComponent<DialogueTracking>();
                frame.anchorMin = new Vector2(.5f, 0f);
                frame.anchorMax = new Vector2(.5f, 0f);
                frame.anchoredPosition = Vector2.zero;
                frame.sizeDelta = new Vector2(1920f, 415f);
                hint.anchorMin = new Vector2(.5f, 0f);
                hint.anchorMax = new Vector2(.5f, 0f);
                hint.anchoredPosition = new Vector2(745f, 85f);
                name.anchoredPosition = new Vector2(-724f, 228f);

                var serialized = new SerializedObject(view);
                serialized.FindProperty("specialText").objectReferenceValue = specialText;
                serialized.FindProperty("useAuthoredTextStyles").boolValue = true;
                serialized.FindProperty("narrationFontSize").intValue = 32;
                serialized.FindProperty("speechFontSize").intValue = 40;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }
    }
}
#endif
