using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using QuietStatic.Toolkit.Editor.Cinematics;
using QuietStatic.Toolkit.Editor.Characters.NPC;
using QuietStatic.Toolkit.Editor.Interactions;
using QuietStatic.Toolkit.Editor.Validation;

namespace QuietStatic.Toolkit.Editor.Tooling
{
    /// <summary>Grouped navigation for project content, loaded scenes, and validation.</summary>
    public sealed class ContentWorkspaceWindow : EditorWindow
    {
        private const string SelectedTabKey = "QuietStatic.ContentWorkspace.SelectedTab";
        private const string SearchKeyPrefix = "QuietStatic.ContentWorkspace.Search.";

        private static readonly (string label, string[] ids)[] Navigation =
        {
            ("Scene", new[] { "scene-content", "scene-flow" }),
            ("Story", new[] { "dialogue", "dialogue-resolvers", "readables", "cinematics", "cinematic-trees", "story-sequences" }),
            ("Game Logic", new[] { "flags", "objectives", "game-states" })
        };

        private readonly Dictionary<string, VisualElement> views = new(StringComparer.Ordinal);
        private readonly List<(ToolbarMenu menu, IContentWorkspaceTab[] tabs)> navigationMenus = new();
        private IReadOnlyList<IContentWorkspaceTab> tabs = Array.Empty<IContentWorkspaceTab>();
        private IContentWorkspaceTab selected;
        private string requestedTab;
        private UnityEngine.Object requestedContext;
        private VisualElement contentHost;
        private ToolbarSearchField search;
        private ToolbarToggle problems;
        private Label heading;
        private Label guidance;

        [MenuItem(QuietStaticMenuPaths.Workspace, false, 1)]
        public static void Open() => GetWindow<ContentWorkspaceWindow>("Content Workspace");

        [MenuItem(QuietStaticMenuPaths.ValidateProject, false, 2)]
        private static void OpenProblems() => Open("problems");

        /// <summary>Opens a workspace view and optionally focuses its asset or scene object.</summary>
        public static void Open(string tabId) => Open(tabId, null);

        internal static void Open(string tabId, UnityEngine.Object context)
        {
            var window = GetWindow<ContentWorkspaceWindow>("Content Workspace");
            if (window.contentHost != null)
            {
                window.SelectRequestedTab(tabId, context);
                return;
            }

            window.requestedTab = tabId;
            window.requestedContext = context;
        }

        public void CreateGUI()
        {
            ResetView();
            rootVisualElement.Clear();
            minSize = new Vector2(440f, 280f);

            tabs = ContentWorkspaceTabDiscovery.Discover();
            BuildNavigation();

            var toolbar = new Toolbar();
            search = new ToolbarSearchField { style = { flexGrow = 1f, flexShrink = 1f } };
            search.RegisterValueChangedCallback(change =>
            {
                if (selected == null) return;
                EditorPrefs.SetString(SearchKeyPrefix + selected.Id, change.newValue);
                selected.SetSearch(change.newValue);
            });
            toolbar.Add(search);
            toolbar.Add(new ToolbarButton(() => selected?.Refresh())
            {
                text = "Refresh",
                tooltip = "Rescan the current view while keeping its search filter."
            });
            AddCommands(toolbar, "Scene Tools", "Open dedicated interaction, cinematic, and scene wiring tools.",
                ("Interaction Explorer", InteractableExplorerWindow.Open),
                ("Clutter Builder", ClutterBuilderWindow.Open),
                ("Clutter Interactables", ClutterInteractableWindow.Open),
                ("Cutscene Explorer", CutsceneExplorerWindow.Open),
                ("Cinematic Launch Routes", CinematicLaunchOverview.Open),
                ("NPC Animation & Behaviors", NPCPerformanceWindow.Open),
                ("Camera Shot Editor", CinematicShotEditorWindow.Open),
                ("Communication Graph", CommunicationGraphWindow.Open),
                ("Export Build Scene Wiring", WiringSnapshotExporter.ExportBuildScenes));
            AddCommands(toolbar, "Narrative JSON",
                "Preview a manifest to import linked content together, import a single dialogue file, or export project content for the narrative authorer.",
                ("Preview Batch Import…", NarrativeBatchJsonImporter.OpenImportPreview),
                ("Import Single Dialogue…", Dialogue.DialogueJsonImporter.ImportWithFilePanel),
                ("Export Project Snapshot…", NarrativeAuthoringJsonExporter.ExportProjectSnapshotWithFolderPanel));
            rootVisualElement.Add(toolbar);

            var header = new VisualElement
            {
                style = { paddingLeft = 8f, paddingRight = 8f, paddingTop = 6f, paddingBottom = 5f }
            };
            heading = new Label
            {
                name = "workspace-view-heading",
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 15f }
            };
            guidance = new Label { style = { whiteSpace = WhiteSpace.Normal, marginTop = 2f } };
            header.Add(heading);
            header.Add(guidance);
            rootVisualElement.Add(header);

            contentHost = new VisualElement { style = { flexGrow = 1f } };
            rootVisualElement.Add(contentHost);

            string tabId = requestedTab ?? EditorPrefs.GetString(SelectedTabKey, "scene-content");
            SelectRequestedTab(tabId, requestedContext);
            requestedTab = null;
            requestedContext = null;
        }

        private void BuildNavigation()
        {
            var toolbar = new Toolbar();
            var grouped = new HashSet<string>(StringComparer.Ordinal) { "problems" };
            foreach (var (label, ids) in Navigation)
            {
                IContentWorkspaceTab[] group = ids.Select(id => tabs.FirstOrDefault(tab => tab.Id == id))
                    .Where(tab => tab != null).ToArray();
                if (group.Length == 0) continue;
                grouped.UnionWith(ids);
                AddNavigationMenu(toolbar, label, group);
            }

            IContentWorkspaceTab[] additional = tabs.Where(tab => !grouped.Contains(tab.Id)).ToArray();
            if (additional.Length > 0) AddNavigationMenu(toolbar, "More", additional);
            IContentWorkspaceTab problemsTab = tabs.FirstOrDefault(tab => tab.Id == "problems");
            if (problemsTab != null)
            {
                problems = new ToolbarToggle { text = problemsTab.DisplayName, tooltip = GuidanceFor(problemsTab.Id) };
                problems.RegisterValueChangedCallback(_ =>
                {
                    Select(problemsTab);
                    problems.SetValueWithoutNotify(true);
                });
                toolbar.Add(problems);
            }
            rootVisualElement.Add(toolbar);
        }

        private void AddNavigationMenu(Toolbar toolbar, string label, IContentWorkspaceTab[] group)
        {
            var menu = new ToolbarMenu { text = label };
            foreach (IContentWorkspaceTab tab in group)
                menu.menu.AppendAction(tab.DisplayName, _ => Select(tab),
                    _ => selected == tab ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            navigationMenus.Add((menu, group));
            toolbar.Add(menu);
        }

        private static void AddCommands(Toolbar toolbar, string label, string tooltip, params (string label, Action run)[] commands)
        {
            var menu = new ToolbarMenu { text = label, tooltip = tooltip };
            foreach (var (name, run) in commands)
                menu.menu.AppendAction(name, _ => run());
            toolbar.Add(menu);
        }

        private void SelectRequestedTab(string tabId, UnityEngine.Object context)
        {
            Select(tabs.FirstOrDefault(tab => tab.Id == tabId) ?? tabs.FirstOrDefault());
            if (selected is AssetCatalogWorkspaceTab catalog && context != null) catalog.RevealAsset(context);
            if (selected is SceneContentWorkspaceTab scene && context is GameObject scope) scene.SetScope(scope);
        }

        private void Select(IContentWorkspaceTab tab)
        {
            if (tab == null || tab == selected) return;
            if (selected != null)
            {
                selected.OnDeselected();
                views[selected.Id].RemoveFromHierarchy();
            }
            selected = tab;
            if (!views.TryGetValue(tab.Id, out VisualElement view))
            {
                view = tab.CreateContent();
                view.style.flexGrow = 1f;
                views.Add(tab.Id, view);
            }
            contentHost.Add(view);

            foreach (var (menu, group) in navigationMenus)
                menu.style.unityFontStyleAndWeight = group.Contains(tab) ? FontStyle.Bold : FontStyle.Normal;
            problems?.SetValueWithoutNotify(tab.Id == "problems");
            heading.text = tab.DisplayName;
            guidance.text = GuidanceFor(tab.Id);
            guidance.style.display = string.IsNullOrEmpty(guidance.text) ? DisplayStyle.None : DisplayStyle.Flex;
            search.tooltip = $"Filter {tab.DisplayName.ToLowerInvariant()}. Each view remembers its own search.";
            string value = EditorPrefs.GetString(SearchKeyPrefix + tab.Id, string.Empty);
            search.SetValueWithoutNotify(value);
            tab.SetSearch(value);
            tab.OnSelected();
            EditorPrefs.SetString(SelectedTabKey, tab.Id);
        }

        private static string GuidanceFor(string id) => id switch
        {
            "scene-content" => "Edit prompts, flavor text, and dialogue references in loaded scenes. Use Selection to include a Hierarchy object and its children.",
            "scene-flow" => "Edit maps that connect scenes, spawn points, and transitions. Open Graph to trace and edit connections visually.",
            "dialogue" => "Edit conversations node by node, or use Open Graph to work with branches. For JSON-generated trees, edit the source and reimport or make an editable copy.",
            "dialogue-resolvers" => "Choose which tree a scene object plays based on flags. The first matching rule wins, with the fallback used when no rule matches.",
            "readables" => "Create notes and other readable content. Assign a definition to a scene object's Readable Interaction Trigger.",
            "cinematics" => "Edit reusable cinematic definitions here. In Scene Tools, use Cinematic Launch Routes to check scene bindings, or Cutscene Explorer to edit sequence steps and dialogue cues.",
            "cinematic-trees" => "Use Cinematic Trees for text-led sequences that play in beat order. Open Linear Graph to arrange beats, preview text timing, and import or export JSON.",
            "story-sequences" => "Edit reusable story beats, their sequence, and the conditions that advance them.",
            "flags" => "Define shared story and gameplay values. Assign the catalogs to the persistent FlagManager and use Safe ID Rename when changing a referenced ID.",
            "objectives" => "Edit player-facing objectives and their activation and completion rules. Add definitions to the database that should use them.",
            "game-states" => "Define the states used by the GameStateManager to control gameplay, pause, and input behavior.",
            "problems" => "Review project asset and loaded scene issues. Select a result to locate its source, then Refresh after making changes.",
            _ => string.Empty
        };

        private void ResetView()
        {
            selected?.OnDeselected();
            selected = null;
            views.Clear();
            navigationMenus.Clear();
            contentHost = null;
            problems = null;
        }

        private void OnDisable() => ResetView();
    }
}
