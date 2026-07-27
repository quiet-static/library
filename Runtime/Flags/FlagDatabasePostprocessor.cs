using UnityEditor;

namespace QuietStatic.Toolkit.Flags.Editor
{
    /// <summary>
    /// Clears the flag dropdown cache whenever project assets change.
    /// </summary>
    public class FlagDatabaseAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsFlagDatabase(importedAssets) ||
                ContainsFlagDatabase(deletedAssets) ||
                ContainsFlagDatabase(movedAssets) ||
                ContainsFlagDatabase(movedFromAssetPaths))
            {
                FlagIdDrawer.ClearCache();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }

        private static bool ContainsFlagDatabase(string[] assetPaths)
        {
            foreach (string path in assetPaths)
            {
                if (path.EndsWith(".asset"))
                {
                    FlagDatabase database =
                        AssetDatabase.LoadAssetAtPath<FlagDatabase>(path);

                    if (database != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}