#if UNITY_EDITOR
using UnityEditor;
namespace MemorialArchive.Editor {
 public static class UpdatedCorridorAssetApplier {
  [MenuItem("Tools/Memorial Archive/Import Updated Corridor Assets")]
  public static void Apply() => LayeredCorridorBuilder.Apply();
 }
}
#endif
