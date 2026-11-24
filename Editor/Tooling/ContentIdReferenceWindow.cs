using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Tooling
{
    internal sealed class ContentIdReferenceWindow : EditorWindow
    {
        private ContentIdChangePreview preview;
        private IReadOnlyList<ContentIdReference> confirmed;
        private IReadOnlyList<ContentIdReference> possible;
        private Action onApplied;
        private Vector2 scroll;

        internal static void PreviewRename(ContentIdChangePreview preview, Action onApplied)
        {
            var window = GetWindow<ContentIdReferenceWindow>(true, "Safe ID Rename");
            window.Configure("Safe ID Rename", preview.References, preview.ManualReview);
            window.preview = preview;
            window.onApplied = onApplied;
        }

        internal static void ShowFlagUsage(IReadOnlyList<ContentIdReference> matches)
        {
            var window = GetWindow<ContentIdReferenceWindow>(true, "Flag Usage");
            window.Configure("Flag Usage", matches.Where(match => match.CanRename).ToArray(),
                matches.Where(match => !match.CanRename).ToArray());
            window.preview = null;
            window.onApplied = null;
        }

        private void Configure(string title, IReadOnlyList<ContentIdReference> confirmed, IReadOnlyList<ContentIdReference> possible)
        {
            titleContent = new GUIContent(title);
            minSize = new Vector2(460f, 280f);
            this.confirmed = confirmed;
            this.possible = possible;
            scroll = Vector2.zero;
            Repaint();
        }

        private void OnGUI()
        {
            if (confirmed == null)
            {
                EditorGUILayout.HelpBox("Run the reference scan again from the Content Workspace.", MessageType.Info);
                return;
            }

            if (preview != null)
                EditorGUILayout.LabelField($"Rename '{preview.OldId}' to '{preview.NewId}'", EditorStyles.wordWrappedLabel);
            else
                EditorGUILayout.LabelField("Project assets, prefabs, and loaded scenes saved under Assets.", EditorStyles.wordWrappedLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawReferences(preview == null ? "Confirmed references" : "References to update", confirmed);
            EditorGUILayout.Space();
            if (preview != null && possible.Count > 0)
                EditorGUILayout.HelpBox("Check each possible reference and edit it by hand if it uses this ID.", MessageType.Info);
            DrawReferences("Possible references for manual review", possible);
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(preview == null ? "Close" : "Cancel")) Close();
                if (preview != null && GUILayout.Button("Apply Rename")) ApplyRename();
            }
        }

        private void DrawReferences(string label, IReadOnlyList<ContentIdReference> references)
        {
            EditorGUILayout.LabelField($"{label} ({references.Count})", EditorStyles.boldLabel);
            if (references.Count == 0) return;
            foreach (ContentIdReference reference in references)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    string details = preview == null ? $"'{reference.OriginalValue.Trim()}'\n{reference.Label}" : reference.Label;
                    EditorGUILayout.LabelField(details, EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button("Locate", GUILayout.Width(58f)))
                        EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(reference.AssetPath));
                }
            }
        }

        private void ApplyRename()
        {
            try { ContentIdChangeService.Apply(preview); }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Rename Failed", exception.Message, "OK");
                return;
            }
            onApplied?.Invoke();
            Close();
        }
    }
}
