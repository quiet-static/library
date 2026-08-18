using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor
{
    /// <summary>
    /// Presents the fully validated effects of a narrative batch before the user confirms import.
    /// </summary>
    public sealed class NarrativeBatchImportWindow : EditorWindow
    {
        [SerializeField] private string manifestPath;
        [SerializeField] private string narrativeOutputFolder =
            NarrativeBatchJsonImporter.DefaultNarrativeOutputFolder;
        [SerializeField] private string dialogueOutputFolder =
            NarrativeBatchJsonImporter.DefaultDialogueOutputFolder;

        private readonly HashSet<string> collapsedDocuments = new(StringComparer.Ordinal);
        private NarrativeBatchJsonImporter.Plan plan;
        private Vector2 scroll;
        private string statusMessage;
        private MessageType statusType = MessageType.Info;
        private bool importInProgress;

        /// <summary>Opens a preview for a manifest or manifest-containing folder.</summary>
        public static void Open(
            string manifestOrFolderPath,
            string narrativeFolder = NarrativeBatchJsonImporter.DefaultNarrativeOutputFolder,
            string dialogueFolder = NarrativeBatchJsonImporter.DefaultDialogueOutputFolder)
        {
            NarrativeBatchImportWindow window =
                GetWindow<NarrativeBatchImportWindow>("Narrative Import");
            window.minSize = new Vector2(620f, 440f);
            window.collapsedDocuments.Clear();
            window.manifestPath = manifestOrFolderPath;
            window.narrativeOutputFolder = narrativeFolder;
            window.dialogueOutputFolder = dialogueFolder;
            try
            {
                window.RefreshPreview(true);
                window.Show();
                window.Focus();
            }
            catch
            {
                window.Close();
                throw;
            }
        }

        private void OnEnable()
        {
            minSize = new Vector2(620f, 440f);
            if (plan == null && !string.IsNullOrWhiteSpace(manifestPath))
                RefreshPreview(false);
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Narrative Import Preview", EditorStyles.largeLabel);
            EditorGUILayout.LabelField(
                "Review the validated Unity asset changes below. No assets are modified until Import is confirmed.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(4f);

            if (!string.IsNullOrWhiteSpace(statusMessage))
                EditorGUILayout.HelpBox(statusMessage, statusType);

            if (plan == null)
            {
                EditorGUILayout.HelpBox(
                    "Choose a valid narrative manifest, then refresh the preview.",
                    MessageType.Info);
                DrawFooter();
                return;
            }

            DrawPaths();
            DrawSummary();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (NarrativeBatchJsonImporter.Document document in plan.Documents)
                DrawDocument(document);
            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(
                        "Choose Manifest...",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(120f)))
                {
                    ChooseManifest();
                }

                using (new EditorGUI.DisabledScope(
                           importInProgress || string.IsNullOrWhiteSpace(manifestPath)))
                {
                    if (GUILayout.Button(
                            "Refresh Preview",
                            EditorStyles.toolbarButton,
                            GUILayout.Width(105f)))
                    {
                        RefreshPreview(false);
                    }
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label("Read-only preflight", EditorStyles.miniLabel);
            }
        }

        private void DrawPaths()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Manifest", plan.ManifestPath, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(
                    "Narrative fallback",
                    plan.NarrativeOutputFolder,
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(
                    "Dialogue fallback",
                    plan.DialogueOutputFolder,
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawSummary()
        {
            int creates = Count(NarrativeBatchJsonImporter.AssetChangeKind.Create);
            int updates = Count(NarrativeBatchJsonImporter.AssetChangeKind.Update);
            int regenerates = Count(NarrativeBatchJsonImporter.AssetChangeKind.Regenerate);
            int deletes = Count(NarrativeBatchJsonImporter.AssetChangeKind.Delete);
            MessageType summaryType = regenerates > 0 || deletes > 0
                ? MessageType.Warning
                : MessageType.Info;
            EditorGUILayout.HelpBox(
                $"{plan.Documents.Count} source document(s), {plan.AssetChanges.Count} asset " +
                $"change(s): {creates} create, {updates} update, " +
                $"{regenerates} regenerate, {deletes} delete.",
                summaryType);

            if (regenerates > 0 || deletes > 0)
            {
                EditorGUILayout.HelpBox(
                    "Regenerated assets receive new GUIDs, and deleted assets are removed. " +
                    "Preflight blocks detected direct external references, but review these rows carefully.",
                    MessageType.Warning);
            }
        }

        private void DrawDocument(NarrativeBatchJsonImporter.Document document)
        {
            NarrativeBatchJsonImporter.AssetChange[] changes = plan.AssetChanges
                .Where(change => ReferenceEquals(change.SourceDocument, document))
                .ToArray();
            bool expanded = !collapsedDocuments.Contains(document.RelativePath);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool nextExpanded = EditorGUILayout.Foldout(
                    expanded,
                    $"{document.Kind}: {document.Identity} ({changes.Length} change(s))",
                    true);
                if (nextExpanded != expanded)
                {
                    if (nextExpanded)
                        collapsedDocuments.Remove(document.RelativePath);
                    else
                        collapsedDocuments.Add(document.RelativePath);
                }

                EditorGUILayout.LabelField(document.RelativePath, EditorStyles.miniLabel);
                if (!nextExpanded)
                    return;

                foreach (NarrativeBatchJsonImporter.AssetChange change in changes)
                    DrawAssetChange(change);
            }
        }

        private static void DrawAssetChange(NarrativeBatchJsonImporter.AssetChange change)
        {
            bool destructive =
                change.Kind == NarrativeBatchJsonImporter.AssetChangeKind.Regenerate ||
                change.Kind == NarrativeBatchJsonImporter.AssetChangeKind.Delete;
            Color previousBackground = GUI.backgroundColor;
            if (destructive)
                GUI.backgroundColor = new Color(1f, 0.72f, 0.35f);
            try
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string actionLabel = destructive
                            ? $"! {change.Kind}"
                            : change.Kind.ToString();
                        GUILayout.Label(
                            new GUIContent(actionLabel, Describe(change.Kind)),
                            EditorStyles.miniBoldLabel,
                            GUILayout.Width(88f));
                        EditorGUILayout.LabelField(
                            $"{change.AssetType.Name}: {change.ContentId}",
                            EditorStyles.boldLabel);

                        UnityEngine.Object existing =
                            AssetDatabase.LoadMainAssetAtPath(change.AssetPath);
                        using (new EditorGUI.DisabledScope(existing == null))
                        {
                            if (GUILayout.Button("Select", GUILayout.Width(55f)))
                            {
                                Selection.activeObject = existing;
                                EditorGUIUtility.PingObject(existing);
                            }
                        }
                    }

                    EditorGUILayout.SelectableLabel(
                        change.AssetPath,
                        EditorStyles.miniLabel,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
            }
            finally
            {
                GUI.backgroundColor = previousBackground;
            }
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(5f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(80f)))
                    Close();

                using (new EditorGUI.DisabledScope(importInProgress || plan == null))
                {
                    string label = plan == null
                        ? "Import"
                        : $"Import {plan.AssetChanges.Count} Change(s)";
                    if (GUILayout.Button(label, GUILayout.Width(150f)))
                        ImportReviewedPlan();
                }
            }
        }

        private void ChooseManifest()
        {
            string initialFolder = string.IsNullOrWhiteSpace(manifestPath)
                ? Directory.GetCurrentDirectory()
                : Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            string selected = EditorUtility.OpenFilePanel(
                "Choose Narrative Authorer Manifest",
                initialFolder,
                "json");
            if (string.IsNullOrWhiteSpace(selected))
                return;

            collapsedDocuments.Clear();
            manifestPath = selected;
            RefreshPreview(false);
        }

        private void RefreshPreview(bool throwOnFailure)
        {
            try
            {
                plan = NarrativeBatchJsonImporter.Preflight(
                    manifestPath,
                    narrativeOutputFolder,
                    dialogueOutputFolder);
                manifestPath = plan.ManifestPath;
                statusMessage = "Preflight passed. Review every asset change before importing.";
                statusType = MessageType.Info;
                Repaint();
            }
            catch (Exception exception)
            {
                plan = null;
                statusMessage = exception.Message;
                statusType = MessageType.Error;
                Repaint();
                if (throwOnFailure)
                    throw;
            }
        }

        private void ImportReviewedPlan()
        {
            importInProgress = true;
            try
            {
                NarrativeBatchJsonImporter.Result result =
                    NarrativeBatchJsonImporter.ImportReviewedPlan(plan);
                UnityEngine.Object selected = result.ImportedAssets.LastOrDefault(
                    asset => asset != null);
                if (selected != null)
                {
                    Selection.activeObject = selected;
                    EditorGUIUtility.PingObject(selected);
                }

                GameLogger.Log(
                    nameof(NarrativeBatchJsonImporter),
                    selected,
                    $"Imported {result.Plan.Documents.Count} narrative source document(s) " +
                    $"with {result.Plan.AssetChanges.Count} asset change(s) from " +
                    $"{result.Plan.ManifestPath}.");
                EditorUtility.DisplayDialog(
                    "Narrative Import Complete",
                    $"Imported {result.Plan.Documents.Count} source document(s) with " +
                    $"{result.Plan.AssetChanges.Count} asset change(s).",
                    "OK");
                Close();
            }
            catch (NarrativeBatchJsonImporter.PreviewOutOfDateException exception)
            {
                RefreshPreview(false);
                if (plan != null)
                {
                    statusMessage = exception.Message;
                    statusType = MessageType.Warning;
                }
            }
            catch (Exception exception)
            {
                GameLogger.Error(
                    nameof(NarrativeBatchJsonImporter),
                    Selection.activeObject,
                    $"Narrative batch import failed: {exception.Message}");
                RefreshPreview(false);
                statusMessage = $"Import failed: {exception.Message}";
                statusType = MessageType.Error;
            }
            finally
            {
                importInProgress = false;
                Repaint();
            }
        }

        private int Count(NarrativeBatchJsonImporter.AssetChangeKind kind) =>
            plan.AssetChanges.Count(change => change.Kind == kind);

        private static string Describe(NarrativeBatchJsonImporter.AssetChangeKind kind)
        {
            return kind switch
            {
                NarrativeBatchJsonImporter.AssetChangeKind.Create =>
                    "Create a new asset at this path.",
                NarrativeBatchJsonImporter.AssetChangeKind.Update =>
                    "Update the existing asset in place and retain its GUID.",
                NarrativeBatchJsonImporter.AssetChangeKind.Regenerate =>
                    "Replace the existing asset with a newly generated asset and GUID.",
                NarrativeBatchJsonImporter.AssetChangeKind.Delete =>
                    "Remove this existing asset during replacement import.",
                _ => string.Empty,
            };
        }
    }
}
