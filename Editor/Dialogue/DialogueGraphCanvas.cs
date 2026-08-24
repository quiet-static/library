using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Tooling;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace QuietStatic.Toolkit.Editor.Dialogue
{
    /// <summary>Editable GraphView projection of a dialogue tree.</summary>
    internal sealed class DialogueGraphCanvas : GraphView
    {
        private sealed class NodeView : Node
        {
            internal NodeView(DialogueGraphNode model, DialogueTree.Node source)
            {
                NodeId = model.StableId;
                title = (model.IsEntry ? "ENTRY • " : string.Empty) + model.StableId;
                viewDataKey = model.StableId;
                Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                Input.portName = "In";
                inputContainer.Add(Input);
                AddOutput("Next", -1);
                DialogueTree.Choice[] choices = source?.choices ?? Array.Empty<DialogueTree.Choice>();
                for (int index = 0; index < choices.Length; index++)
                    AddOutput(string.IsNullOrWhiteSpace(choices[index]?.text) ? $"Choice {index + 1}" : choices[index].text, index);
                extensionContainer.Add(new Label(string.IsNullOrWhiteSpace(model.Speaker) ? "<No speaker>" : model.Speaker));
                extensionContainer.Add(new Label(string.IsNullOrWhiteSpace(model.Line) ? "<Empty line>" : model.Line));
                RefreshExpandedState();
                RefreshPorts();
            }

            internal string NodeId { get; }
            internal Port Input { get; }
            internal Dictionary<int, Port> Outputs { get; } = new();

            private void AddOutput(string label, int choiceIndex)
            {
                Port port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                port.portName = label;
                port.userData = choiceIndex;
                Outputs[choiceIndex] = port;
                outputContainer.Add(port);
            }
        }

        private DialogueTree tree;
        private bool rebuilding;

        internal DialogueGraphCanvas()
        {
            style.flexGrow = 1f;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            graphViewChanged = HandleGraphChanged;
        }

        internal event Action Changed;
        internal Func<string, bool> DeleteNodeRequested;

        internal bool FrameNode(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return false;
            NodeView match = nodes.OfType<NodeView>().FirstOrDefault(node =>
                node.NodeId.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0 ||
                node.title.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
            if (match == null) return false;
            ClearSelection();
            AddToSelection(match);
            FrameSelection();
            return true;
        }

        internal void Populate(DialogueTree value, DialogueGraphModel model)
        {
            rebuilding = true;
            try
            {
                tree = value;
                DeleteElements(graphElements.ToList());
                if (tree == null || model == null) return;
                var views = new Dictionary<string, NodeView>(StringComparer.Ordinal);
                foreach (DialogueGraphNode node in model.Nodes)
                {
                    var view = new NodeView(node, tree.Nodes[node.Index]);
                    Vector2 position = GraphLayoutStore.instance.TryGetPosition(tree, node.StableId, out Vector2 saved)
                        ? saved : new Vector2((node.Index % 4) * 310f, (node.Index / 4) * 210f);
                    view.SetPosition(new Rect(position, new Vector2(250f, 150f)));
                    AddElement(view);
                    views[node.StableId] = view;
                }
                foreach (DialogueGraphEdge edge in model.Edges)
                {
                    if (edge.IsBroken || !views.TryGetValue(edge.SourceId, out NodeView source) ||
                        !views.TryGetValue(edge.TargetId, out NodeView target) ||
                        !source.Outputs.TryGetValue(edge.ChoiceIndex, out Port output)) continue;
                    Edge view = output.ConnectTo(target.Input);
                    view.userData = edge.Id;
                    AddElement(view);
                }
            }
            finally { rebuilding = false; }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) =>
            ports.Where(port => port != startPort && port.node != startPort.node && port.direction != startPort.direction).ToList();

        private GraphViewChange HandleGraphChanged(GraphViewChange change)
        {
            if (rebuilding || tree == null) return change;
            foreach (NodeView node in change.movedElements?.OfType<NodeView>() ?? Enumerable.Empty<NodeView>())
                GraphLayoutStore.instance.SetPosition(tree, node.NodeId, node.GetPosition().position);

            foreach (NodeView node in change.elementsToRemove?.OfType<NodeView>() ?? Enumerable.Empty<NodeView>())
                DeleteNodeRequested?.Invoke(node.NodeId);

            Edge[] removed = change.elementsToRemove?.OfType<Edge>().ToArray() ?? Array.Empty<Edge>();
            Edge[] created = change.edgesToCreate?.ToArray() ?? Array.Empty<Edge>();
            foreach (Edge edge in removed)
            {
                if (created.Length > 0) continue;
                SetTarget(edge.output, null);
            }
            foreach (Edge edge in created)
                SetTarget(edge.output, (edge.input.node as NodeView)?.NodeId);
            Changed?.Invoke();
            return change;
        }

        private void SetTarget(Port output, string targetId)
        {
            if (tree.GeneratedFromJson || output?.node is not NodeView source) return;
            int choice = output.userData is int value ? value : -1;
            if (choice < 0) DialogueGraphCommands.ReconnectLinear(tree, source.NodeId, targetId);
            else DialogueGraphCommands.ReconnectChoice(tree, source.NodeId, choice, targetId);
        }
    }
}
