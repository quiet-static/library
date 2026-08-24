using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using QuietStatic.Toolkit.Editor.Cinematics;
using QuietStatic.Toolkit.Editor.Interactions;
using QuietStatic.Toolkit.Editor.Validation;

namespace QuietStatic.Toolkit.Editor.Tooling
{
    /// <summary>Single tabbed home for project content databases and definitions.</summary>
    public sealed class ContentWorkspaceWindow : EditorWindow
    {
        private const string SelectedTabKey = "QuietStatic.ContentWorkspace.SelectedTab";
        private static string requestedTab;
        private readonly List<(IContentWorkspaceTab tab, ToolbarToggle toggle, VisualElement content)> views = new();
        private IContentWorkspaceTab selected;
        private VisualElement contentHost;
        private ToolbarSearchField search;
        private readonly Dictionary<string, string> searches = new(StringComparer.Ordinal);

        [MenuItem(QuietStaticMenuPaths.Workspace, false, 1)]
        public static void Open() => GetWindow<ContentWorkspaceWindow>("Content Workspace");

        [MenuItem(QuietStaticMenuPaths.ValidateProject, false, 2)]
        private static void OpenProblems() => Open("problems");

        public static void Open(string tabId)
        {
            requestedTab = tabId;
            GetWindow<ContentWorkspaceWindow>("Content Workspace");
        }

        public void CreateGUI()
        {
            var toolbar = new Toolbar();
            search = new ToolbarSearchField { style = { minWidth = 180f } };
            search.RegisterValueChangedCallback(change =>
            {
                if (selected == null) return;
                searches[selected.Id] = change.newValue;
                EditorPrefs.SetString($"QuietStatic.ContentWorkspace.Search.{selected.Id}", change.newValue);
                selected.SetSearch(change.newValue);
            });
            toolbar.Add(search);
            toolbar.Add(new ToolbarButton(() => selected?.Refresh()) { text = "Refresh" });
            toolbar.Add(new ToolbarButton(NarrativeBatchJsonImporter.OpenImportPreview) { text = "Import Batch" });
            toolbar.Add(new ToolbarButton(NarrativeAuthoringJsonExporter.ExportProjectSnapshotWithFolderPanel) { text = "Export Snapshot" });
            toolbar.Add(new ToolbarButton(InteractableExplorerWindow.Open) { text = "Interactions" });
            toolbar.Add(new ToolbarButton(CutsceneExplorerWindow.Open) { text = "Cutscenes" });
            toolbar.Add(new ToolbarButton(CommunicationGraphWindow.Open) { text = "Communication" });
            toolbar.Add(new ToolbarButton(WiringSnapshotExporter.ExportBuildScenes) { text = "Export Wiring" });
            rootVisualElement.Add(toolbar);

            var tabs = new Toolbar();
            rootVisualElement.Add(tabs);
            contentHost = new VisualElement { style = { flexGrow = 1f } };
            rootVisualElement.Add(contentHost);

            IReadOnlyList<IContentWorkspaceTab> discovered = ContentWorkspaceTabDiscovery.Discover();
            foreach (IContentWorkspaceTab tab in discovered)
            {
                VisualElement content = tab.CreateContent();
                content.style.flexGrow = 1f;
                content.style.display = DisplayStyle.None;
                var toggle = new ToolbarToggle { text = tab.DisplayName };
                toggle.RegisterValueChangedCallback(change => { if (change.newValue) Select(tab); });
                tabs.Add(toggle);
                contentHost.Add(content);
                views.Add((tab, toggle, content));
            }

            string saved = string.IsNullOrWhiteSpace(requestedTab)
                ? EditorPrefs.GetString(SelectedTabKey, string.Empty)
                : requestedTab;
            requestedTab = null;
            Select(discovered.FirstOrDefault(tab => tab.Id == saved) ?? discovered.FirstOrDefault());
        }

        private void Select(IContentWorkspaceTab tab)
        {
            if (tab == null || tab == selected) return;
            selected?.OnDeselected();
            selected = tab;
            foreach ((IContentWorkspaceTab candidate, ToolbarToggle toggle, VisualElement content) in views)
            {
                bool active = candidate == selected;
                toggle.SetValueWithoutNotify(active);
                content.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (!searches.TryGetValue(selected.Id, out string value))
                searches[selected.Id] = value = EditorPrefs.GetString($"QuietStatic.ContentWorkspace.Search.{selected.Id}", string.Empty);
            search.SetValueWithoutNotify(value);
            selected.SetSearch(value);
            selected.OnSelected();
            EditorPrefs.SetString(SelectedTabKey, selected.Id);
        }

        private void OnDisable() => selected?.OnDeselected();
    }
}
