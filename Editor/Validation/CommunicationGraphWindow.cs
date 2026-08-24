using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Validation
{
    /// <summary>Read-only explorer for loaded-scene communication references and diagnostics.</summary>
    public sealed class CommunicationGraphWindow : EditorWindow
    {
        private CommunicationGraph graph = new(Array.Empty<CommunicationNode>(), Array.Empty<CommunicationEdge>());
        private Vector2 scroll;
        private string search = string.Empty;
        private bool diagnosticsOnly;
        private bool crossSceneOnly;
        private bool scanSelection;

        public static void Open() => GetWindow<CommunicationGraphWindow>("Communication Graph");

        private void OnEnable() => Refresh();

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f))) Refresh();
                bool nextSelection = GUILayout.Toggle(scanSelection, "Selection", EditorStyles.toolbarButton, GUILayout.Width(72f));
                if (nextSelection != scanSelection)
                {
                    scanSelection = nextSelection;
                    Refresh();
                }
                diagnosticsOnly = GUILayout.Toggle(diagnosticsOnly, "Problems", EditorStyles.toolbarButton, GUILayout.Width(70f));
                crossSceneOnly = GUILayout.Toggle(crossSceneOnly, "Cross-scene", EditorStyles.toolbarButton, GUILayout.Width(84f));
                search = GUILayout.TextField(search, EditorStyles.toolbarSearchField);
            }

            EditorGUILayout.LabelField($"{graph.Nodes.Count} nodes, {graph.Edges.Count} edges", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (CommunicationEdge edge in graph.Edges.Where(Matches))
            {
                CommunicationNode source = graph.Nodes.First(node => node.Id == edge.SourceId);
                CommunicationNode target = graph.Nodes.First(node => node.Id == edge.TargetId);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(source.Label, EditorStyles.linkLabel, GUILayout.Width(position.width * 0.36f))) Select(source.Context);
                    GUILayout.Label($"— {edge.Kind}: {edge.PropertyPath} →", GUILayout.Width(position.width * 0.25f));
                    if (GUILayout.Button(target.Label, EditorStyles.linkLabel)) Select(target.Context);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private bool Matches(CommunicationEdge edge)
        {
            CommunicationNode source = graph.Nodes.First(node => node.Id == edge.SourceId);
            CommunicationNode target = graph.Nodes.First(node => node.Id == edge.TargetId);
            if (diagnosticsOnly && source.Kind != CommunicationNodeKind.Diagnostic && target.Kind != CommunicationNodeKind.Diagnostic) return false;
            if (crossSceneOnly && !CrossesSceneBoundary(source.Context, target.Context)) return false;
            return string.IsNullOrWhiteSpace(search) ||
                   source.Label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   target.Label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   edge.PropertyPath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool CrossesSceneBoundary(UnityEngine.Object source, UnityEngine.Object target)
        {
            Component sourceComponent = source as Component;
            Component targetComponent = target as Component;
            return sourceComponent != null &&
                   targetComponent != null &&
                   sourceComponent.gameObject.scene.IsValid() &&
                   targetComponent.gameObject.scene.IsValid() &&
                   sourceComponent.gameObject.scene != targetComponent.gameObject.scene;
        }

        private void Refresh()
        {
            graph = scanSelection
                ? CommunicationGraphExtractor.ScanSelection()
                : CommunicationGraphExtractor.ScanLoadedScenes();
            Repaint();
        }

        private static void Select(UnityEngine.Object context)
        {
            if (context == null) return;
            Selection.activeObject = context;
            EditorGUIUtility.PingObject(context);
        }
    }
}
