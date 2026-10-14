using System;
using System.IO;
using QuietStatic.Toolkit.Cinematics;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Cinematics
{
    /// <summary>Imports Cinematic Tree JSON while preserving an existing asset GUID.</summary>
    public static class CinematicTreeJsonImporter
    {
        public static void ValidateJson(string json)
        {
            var temporary = ScriptableObject.CreateInstance<CinematicTree>();
            try { CinematicTreeCommands.ImportJson(temporary, json); }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        public static CinematicTree Import(TextAsset source, string outputFolder)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!NarrativeJsonPathUtility.IsCanonicalAssetFolder(outputFolder))
                throw new ArgumentException("Output folder must be a project-relative path under Assets.", nameof(outputFolder));
            ValidateJson(source.text);
            string identity = Path.GetFileNameWithoutExtension(source.name);
            string assetPath = $"{outputFolder.TrimEnd('/')}/Cinematics/{MakeSafeFileName(identity)}.asset";
            NarrativeJsonPathUtility.EnsureAssetFolderForPath(assetPath);
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (existing != null && existing is not CinematicTree)
                throw new ArgumentException($"Cannot import CinematicTree to '{assetPath}' because it contains {existing.GetType().Name}.");
            CinematicTree tree = AssetDatabase.LoadAssetAtPath<CinematicTree>(assetPath);
            bool created = tree == null;
            if (created)
            {
                tree = ScriptableObject.CreateInstance<CinematicTree>();
                AssetDatabase.CreateAsset(tree, assetPath);
            }
            try
            {
                CinematicTreeCommands.ImportJson(tree, source.text);
                AssetDatabase.SaveAssetIfDirty(tree);
                return tree;
            }
            catch
            {
                if (created) AssetDatabase.DeleteAsset(assetPath);
                throw;
            }
        }

        private static string MakeSafeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Trim();
        }
    }
}
