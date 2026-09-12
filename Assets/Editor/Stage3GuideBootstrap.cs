#if UNITY_EDITOR
using UnityEditor;
namespace MemorialArchive.Editor
{
    public static class Stage3GuideBootstrap
    {
        [MenuItem("Tools/Memorial Archive/Build Stage 3 Guide")]
        public static void BuildStage3Guide() => GuidePresentationBuilder.Build();
    }
}
#endif
