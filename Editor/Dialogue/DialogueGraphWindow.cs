using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Dialogue;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Dialogue
{
    /// <summary>Read-only, pannable graph for the existing DialogueTree data model.</summary>
    public sealed class DialogueGraphWindow : EditorWindow
    {
        private const float NodeWidth = 230f;
        private const float NodeHeight = 112f;
        private DialogueTree tree;
        private DialogueGraphModel model;
        private readonly Dictionary<int, Rect> nodeRects = new();
        private Vector2 pan = new(35f, 35f);
        private int selectedIndex = -1;

        [MenuItem("Tools/Narrative/Dialogue Graph")]
        public static void Open()
        {
            DialogueGraphWindow window = GetWindow<DialogueGraphWindow>("Dialogue Graph");
            window.UseSelection();
        }

        [MenuItem("Assets/Open in Dialogue Graph", true)]
        private static bool ValidateOpenSelected() => Selection.activeObject is DialogueTree;

        [MenuItem("Assets/Open in Dialogue Graph")]
        private static void OpenSelected()
        {
            DialogueGraphWindow window = GetWindow<DialogueGraphWindow>("Dialogue Graph");
            window.SetTree(Selection.activeObject as DialogueTree);
        }

        private void OnEnable()
        {
            if (tree == null)
            {
                UseSelection();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            Rect canvas = GUILayoutUtility.GetRect(0f, 100000f, 0f, 100000f);
            GUI.Box(canvas, GUIContent.none, EditorStyles.helpBox);

            if (tree == null || model == null)
            {
                GUI.Label(new Rect(canvas.x + 20f, canvas.y + 20f, 500f, 40f),
                    "Select a DialogueTree asset, then choose Assets > Open in Dialogue Graph.",
                    EditorStyles.wordWrappedLabel);
                return;
            }

            HandlePan(canvas);
            EnsureLayout();
            DrawEdges(canvas);
            DrawNodes(canvas);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                DialogueTree next = (DialogueTree)EditorGUILayout.ObjectField(
                    tree, typeof(DialogueTree), false, GUILayout.Width(240f));
                if (EditorGUI.EndChangeCheck())
                {
                    SetTree(next);
                }

                if (GUILayout.Button("Frame All", EditorStyles.toolbarButton, GUILayout.Width(75f)))
                {
                    pan = new Vector2(35f, 35f);
                }
                if (GUILayout.Button("Select Asset", EditorStyles.toolbarButton, GUILayout.Width(85f)) &&
                    tree != null)
                {
                    Selection.activeObject = tree;
                    EditorGUIUtility.PingObject(tree);
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label("Read-only • drag background to pan", EditorStyles.miniLabel);
            }
        }

        private void HandlePan(Rect canvas)
        {
            Event current = Event.current;
            if (current.type == EventType.MouseDrag &&
                (current.button == 0 || current.button == 2) &&
                canvas.Contains(current.mousePosition))
            {
                pan += current.delta;
                current.Use();
                Repaint();
            }
        }

        private void EnsureLayout()
        {
            if (nodeRects.Count == model.Nodes.Count)
            {
                return;
            }

            nodeRects.Clear();
            const int columns = 4;
            for (int index = 0; index < model.Nodes.Count; index++)
            {
                int column = index % columns;
                int row = index / columns;
                nodeRects[index] = new Rect(
                    column * (NodeWidth + 80f),
                    row * (NodeHeight + 80f),
                    NodeWidth,
                    NodeHeight);
            }
        }

        private void DrawEdges(Rect canvas)
        {
            Handles.BeginGUI();
            foreach (DialogueGraphEdge edge in model.Edges)
            {
                if (!nodeRects.TryGetValue(edge.SourceIndex, out Rect source))
                {
                    continue;
                }

                Vector2 start = canvas.position + pan + new Vector2(source.xMax, source.center.y);
                Color color = edge.IsBroken ? new Color(1f, 0.3f, 0.25f) : new Color(0.55f, 0.7f, 0.9f);
                if (edge.IsBroken || !nodeRects.TryGetValue(edge.TargetIndex, out Rect target))
                {
                    Handles.color = color;
                    Handles.DrawLine(start, start + Vector2.right * 45f);
                    continue;
                }

                Vector2 end = canvas.position + pan + new Vector2(target.xMin, target.center.y);
                float tangent = Mathf.Max(45f, Mathf.Abs(end.x - start.x) * 0.35f);
                Handles.DrawBezier(start, end, start + Vector2.right * tangent,
                    end + Vector2.left * tangent, color, null, 2f);
            }
            Handles.EndGUI();
        }

        private void DrawNodes(Rect canvas)
        {
            foreach (DialogueGraphNode node in model.Nodes)
            {
                Rect rect = nodeRects[node.Index];
                rect.position += canvas.position + pan;
                Color previous = GUI.backgroundColor;
                bool unreachable = model.UnreachableNodeIndexes.Contains(node.Index);
                GUI.backgroundColor = node.Index == selectedIndex
                    ? new Color(0.55f, 0.8f, 1f)
                    : node.IsEntry ? new Color(0.55f, 1f, 0.65f)
                    : unreachable ? new Color(1f, 0.7f, 0.45f) : Color.white;

                if (GUI.Button(rect, GUIContent.none, EditorStyles.helpBox))
                {
                    selectedIndex = node.Index;
                    Repaint();
                }

                GUI.backgroundColor = previous;
                GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 20f),
                    $"{(node.IsEntry ? "ENTRY • " : string.Empty)}{node.StableId}",
                    EditorStyles.boldLabel);
                GUI.Label(new Rect(rect.x + 8f, rect.y + 28f, rect.width - 16f, 18f),
                    string.IsNullOrWhiteSpace(node.Speaker) ? "<No Speaker>" : node.Speaker,
                    EditorStyles.miniBoldLabel);
                GUI.Label(new Rect(rect.x + 8f, rect.y + 49f, rect.width - 16f, 56f),
                    string.IsNullOrWhiteSpace(node.Line) ? "<Empty dialogue text>" : node.Line,
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void UseSelection()
        {
            if (Selection.activeObject is DialogueTree selected)
            {
                SetTree(selected);
            }
        }

        private void SetTree(DialogueTree value)
        {
            tree = value;
            model = tree == null ? null : DialogueGraphModelBuilder.Build(tree);
            selectedIndex = -1;
            nodeRects.Clear();
            pan = new Vector2(35f, 35f);
            Repaint();
        }
    }
}
