using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Gameplay.Dialogue.View
{
    // The two fonts have different advance widths. Position inline glyphs from
    // the body layout rather than laying out a transparent prefix in the other font.
    [RequireComponent(typeof(Text))]
    public sealed class DialogueInlinePosition : BaseMeshEffect
    {
        public Text Body;
        public int StartCharacter;

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || Body == null) return;
            var overlay = GetComponent<Text>();
            var source = Body.cachedTextGenerator.characters;
            var own = overlay.cachedTextGenerator.characters;
            var lines = Body.cachedTextGenerator.lines;
            var sourceText = Body.text ?? string.Empty;
            var visibleCount = CountVisibleCharacters(sourceText);
            // Depending on Unity version, TextGenerator.characters either keeps
            // rich-text tag slots or strips them. Resolve the mode before using
            // StartCharacter, which is always a visible-character offset.
            var keepsTags = source.Count != visibleCount;
            var tracking = Body.GetComponent<DialogueTracking>();
            var spacing = (tracking != null ? tracking.Tracking : 0f) * Body.fontSize / 1000f;
            var vertex = new UIVertex();
            for (var index = 0; index < mesh.currentVertCount / 4 && index < own.Count; index++)
            {
                var visibleIndex = StartCharacter + index;
                var sourceIndex = keepsTags
                    ? RawIndexForVisibleCharacter(sourceText, visibleIndex)
                    : visibleIndex;
                if (sourceIndex >= source.Count) break;
                var line = 0;
                while (line + 1 < lines.Count && sourceIndex >= lines[line + 1].startCharIdx) line++;
                var start = lines.Count > 0 ? lines[line].startCharIdx : 0;
                var delta = source[sourceIndex].cursorPos / Body.pixelsPerUnit - own[index].cursorPos / overlay.pixelsPerUnit;
                var lineVisibleStart = keepsTags
                    ? VisibleCharactersBeforeRawIndex(sourceText, start)
                    : start;
                delta.x += (visibleIndex - lineVisibleStart) * spacing;
                for (var corner = 0; corner < 4; corner++)
                {
                    mesh.PopulateUIVertex(ref vertex, index * 4 + corner);
                    vertex.position += new Vector3(delta.x, delta.y, 0f);
                    mesh.SetUIVertex(vertex, index * 4 + corner);
                }
            }
        }

        private static int CountVisibleCharacters(string value)
        {
            var count = 0;
            var inTag = false;
            foreach (var c in value)
            {
                if (c == '<') { inTag = true; continue; }
                if (inTag) { if (c == '>') inTag = false; continue; }
                count++;
            }
            return count;
        }

        private static int RawIndexForVisibleCharacter(string value, int visibleIndex)
        {
            var visible = 0;
            var inTag = false;
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == '<') { inTag = true; continue; }
                if (inTag) { if (value[i] == '>') inTag = false; continue; }
                if (visible++ == visibleIndex) return i;
            }
            return value.Length;
        }

        private static int VisibleCharactersBeforeRawIndex(string value, int rawIndex)
        {
            var limit = Mathf.Clamp(rawIndex, 0, value.Length);
            var count = 0;
            var inTag = false;
            for (var i = 0; i < limit; i++)
            {
                if (value[i] == '<') { inTag = true; continue; }
                if (inTag) { if (value[i] == '>') inTag = false; continue; }
                count++;
            }
            return count;
        }
    }
}
