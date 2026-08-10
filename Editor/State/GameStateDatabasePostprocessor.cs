using QuietStatic.Toolkit.Core;
using UnityEditor;

namespace QuietStatic.Toolkit.Editor.State
{
    /// <summary>Invalidates state dropdown data after database asset changes.</summary>
    public sealed class GameStateDatabasePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsDatabase(importedAssets) ||
                ContainsDatabase(deletedAssets) ||
                ContainsDatabase(movedAssets) ||
                ContainsDatabase(movedFromAssetPaths))
            {
                GameStateIdDrawer.ClearCache();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }

        private static bool ContainsDatabase(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path.EndsWith(".asset") &&
                    AssetDatabase.LoadAssetAtPath<GameStateDatabase>(path) != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
