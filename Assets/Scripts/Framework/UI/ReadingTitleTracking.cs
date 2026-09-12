using UnityEngine;
using UnityEngine.UI;

namespace MemorialArchive.Framework.UI
{
    // The artwork specifies tracking 12 (12/1000 em), not a 12-pixel gap.
    [RequireComponent(typeof(Text))]
    public sealed class ReadingTitleTracking : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            var text = GetComponent<Text>();
            var lines = text.cachedTextGenerator.lines;
            var line = 0;
            var vertex = new UIVertex();
            for (var character = 0; character < mesh.currentVertCount / 4; character++)
            {
                while (line + 1 < lines.Count && character >= lines[line + 1].startCharIdx) line++;
                var offset = (character - (lines.Count > 0 ? lines[line].startCharIdx : 0)) * text.fontSize * .012f;
                for (var corner = 0; corner < 4; corner++)
                {
                    var index = character * 4 + corner;
                    mesh.PopulateUIVertex(ref vertex, index);
                    vertex.position.x += offset;
                    mesh.SetUIVertex(vertex, index);
                }
            }
        }
    }
}
