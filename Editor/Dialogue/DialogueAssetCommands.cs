using System;
using QuietStatic.Toolkit.Dialogue;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Dialogue
{
    /// <summary>Asset-level dialogue operations that keep generated sources read-only.</summary>
    public static class DialogueAssetCommands
    {
        /// <summary>Creates an editable asset copy detached from any generated JSON source.</summary>
        public static DialogueTree CreateEditableCopy(DialogueTree source, string assetPath)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(assetPath)) throw new ArgumentException("Asset path is required.", nameof(assetPath));
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            DialogueTree copy = UnityEngine.Object.Instantiate(source);
            copy.name = System.IO.Path.GetFileNameWithoutExtension(uniquePath);
            var serialized = new SerializedObject(copy);
            serialized.FindProperty("generatedFromJson").boolValue = false;
            serialized.FindProperty("sourceJsonPath").stringValue = string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(copy, uniquePath);
            AssetDatabase.SaveAssetIfDirty(copy);
            return copy;
        }
    }
}
