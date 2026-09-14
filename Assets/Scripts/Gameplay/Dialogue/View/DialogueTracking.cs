using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Gameplay.Dialogue.View
{
    /// <summary>Applies authored tracking in thousandths of an em per wrapped line.</summary>
    [RequireComponent(typeof(Text))]
    public sealed class DialogueTracking : BaseMeshEffect
    {
        [SerializeField] private float tracking = 12f;

        public float Tracking
        {
            get => tracking;
            set { tracking = value; graphic?.SetVerticesDirty(); }
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            var text = GetComponent<Text>();
            var lines = text.cachedTextGenerator.lines;
            var sourceText = text.text ?? string.Empty;
            var visibleCount = CountVisibleCharacters(sourceText);
            var keepsTags = text.cachedTextGenerator.characters.Count != visibleCount;
            var line = 0;
            var vertex = new UIVertex();
            var em = tracking / 1000f;
            for (var character = 0; character < mesh.currentVertCount / 4; character++)
            {
                var nextLineStart = line + 1 < lines.Count ? lines[line + 1].startCharIdx : int.MaxValue;
                while (line + 1 < lines.Count && character >= (keepsTags
                    ? VisibleCharactersBeforeRawIndex(sourceText, nextLineStart)
                    : nextLineStart))
                {
                    line++;
                    nextLineStart = line + 1 < lines.Count ? lines[line + 1].startCharIdx : int.MaxValue;
                }
                var start = lines.Count > 0 ? (keepsTags
                    ? VisibleCharactersBeforeRawIndex(sourceText, lines[line].startCharIdx)
                    : lines[line].startCharIdx) : 0;
                var end = nextLineStart == int.MaxValue
                    ? mesh.currentVertCount / 4
                    : (keepsTags ? VisibleCharactersBeforeRawIndex(sourceText, nextLineStart) : nextLineStart);
                var lineCharacters = Mathf.Max(1, end - start);
                var centered = text.alignment == TextAnchor.MiddleCenter
                    || text.alignment == TextAnchor.UpperCenter
                    || text.alignment == TextAnchor.LowerCenter;
                var centerCompensation = centered
                    ? (lineCharacters - 1) * text.fontSize * em * .5f : 0f;
                var offset = (character - start) * text.fontSize * em - centerCompensation;
                for (var corner = 0; corner < 4; corner++)
                {
                    var index = character * 4 + corner;
                    mesh.PopulateUIVertex(ref vertex, index);
                    vertex.position.x += offset;
                    mesh.SetUIVertex(vertex, index);
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
