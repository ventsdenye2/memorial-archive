using System.Linq;
using System.Reflection;
using MemorialArchive.Gameplay.Dialogue.View;
using MemorialArchive.Framework.UI;
using MemorialArchive.Gameplay.Story.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Tests.Editor
{
    public sealed class Feedback0913Tests
    {
        [Test]
        public void NarrativeText_AllLinesRemainCompleteAndFitAtEightPoints()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/NarrativePanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var view = instance.GetComponentsInChildren<Text>(true).Single(t => t.name == "Text");
                Assert.That(view.fontSize, Is.EqualTo(32));
                Assert.That(view.font.name, Is.EqualTo("FZCHSJW"));
                foreach (var line in NarrativeContent.Load().sequences.SelectMany(s => s.lines))
                {
                    var content = string.IsNullOrEmpty(line.speaker) ? line.text : line.speaker + "：" + line.text;
                    var pages = DiaryPanel.Paginate(content, view);
                    Assert.That(string.Concat(pages), Is.EqualTo(content));
                    foreach (var page in pages)
                    {
                        var height = new TextGenerator().GetPreferredHeight(page, view.GetGenerationSettings(view.rectTransform.rect.size)) / view.pixelsPerUnit;
                        Assert.That(height, Is.LessThanOrEqualTo(view.rectTransform.rect.height), page);
                    }
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void DialogueInlinePosition_MapsVisibleCharactersPastRichTextTags()
        {
            const string text = "前缀<size=32><i><color=#FFFFFF>一步步</color></i></size>后缀";
            var method = typeof(DialogueInlinePosition).GetMethod(
                "RawIndexForVisibleCharacter", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            var firstSpecial = (int)method.Invoke(null, new object[] { text, 2 });
            var secondSpecial = (int)method.Invoke(null, new object[] { text, 3 });
            Assert.That(text[firstSpecial], Is.EqualTo('一'));
            Assert.That(text[secondSpecial], Is.EqualTo('步'));
            Assert.That(secondSpecial, Is.GreaterThan(firstSpecial));
        }
    }
}
