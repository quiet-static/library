using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Editor.Tooling;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace QuietStatic.Toolkit.Editor.SceneFlow
{
    /// <summary>GraphView adapter for the UI-independent scene-flow model and commands.</summary>
    internal sealed class SceneFlowGraphCanvas : GraphView
    {
        private sealed class SceneNodeView : Node
        {
            internal SceneNodeView(SceneFlowGraphNode model)
            {
                SceneId = model.Id;
                title = model.Id;
                viewDataKey = model.Id;
                userData = model.Id;
                Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                Output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
                Input.portName = "In";
                Output.portName = "Out";
                inputContainer.Add(Input);
                outputContainer.Add(Output);
                if (model.IsEntry) title = $"ENTRY • {title}";
                if (model.IsDeadEnd) Add(new Label("Dead end"));
                if (!model.IsBuilt) Add(new Label("Not in Build Settings"));
                if (!model.IsReachable) Add(new Label("Unreachable"));
                RefreshExpandedState();
                RefreshPorts();
            }

            internal string SceneId { get; }
            internal Port Input { get; }
            internal Port Output { get; }
        }

        private SceneFlowMap map;
        private bool rebuilding;

        internal SceneFlowGraphCanvas()
        {
            style.flexGrow = 1f;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            graphViewChanged = HandleGraphChanged;
        }

        internal event Action Changed;
        internal Func<string, bool> DeleteRequested;

        internal void Populate(SceneFlowMap value, SceneFlowGraphModel model)
        {
            rebuilding = true;
            try
            {
                map = value;
                DeleteElements(graphElements.ToList());
                if (map == null || model == null) return;

                var views = new Dictionary<string, SceneNodeView>(StringComparer.Ordinal);
                int index = 0;
                foreach (SceneFlowGraphNode node in model.Nodes)
                {
                    var view = new SceneNodeView(node);
                    Vector2 position = GraphLayoutStore.instance.TryGetPosition(map, node.Id, out Vector2 saved)
                        ? saved
                        : new Vector2((index % 4) * 310f, (index / 4) * 190f);
                    view.SetPosition(new Rect(position, new Vector2(250f, 130f)));
                    AddElement(view);
                    views[node.Id] = view;
                    index++;
                }

                foreach (SceneFlowGraphEdge edge in model.Edges)
                {
                    if (!views.TryGetValue(edge.SourceId, out SceneNodeView source) ||
                        !views.TryGetValue(edge.TargetId, out SceneNodeView target)) continue;
                    Edge view = source.Output.ConnectTo(target.Input);
                    view.userData = edge.Id;
                    view.viewDataKey = edge.Id;
                    AddElement(view);
                }
            }
            finally
            {
                rebuilding = false;
            }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) =>
            ports.Where(port => port != startPort && port.node != startPort.node &&
                                port.direction != startPort.direction).ToList();

        private GraphViewChange HandleGraphChanged(GraphViewChange change)
        {
            if (rebuilding || map == null) return change;

            if (change.movedElements != null)
            {
                foreach (SceneNodeView node in change.movedElements.OfType<SceneNodeView>())
                    GraphLayoutStore.instance.SetPosition(map, node.SceneId, node.GetPosition().position);
            }

            Edge[] removedEdges = change.elementsToRemove?.OfType<Edge>().ToArray() ?? Array.Empty<Edge>();
            Edge[] createdEdges = change.edgesToCreate?.ToArray() ?? Array.Empty<Edge>();
            Edge removedEdge = removedEdges.Length == 1 ? removedEdges[0] : null;
            Edge createdEdge = createdEdges.Length == 1 ? createdEdges[0] : null;
            bool reconnecting = removedEdge != null && createdEdge != null &&
                                !string.IsNullOrWhiteSpace(removedEdge.userData as string);

            if (reconnecting)
            {
                string source = (createdEdge.output.node as SceneNodeView)?.SceneId;
                string target = (createdEdge.input.node as SceneNodeView)?.SceneId;
                string id = (string)removedEdge.userData;
                if (!string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(target))
                {
                    SceneFlowGraphCommands.Reconnect(map, id, source, target);
                    createdEdge.userData = id;
                    createdEdge.viewDataKey = id;
                }
            }
            else if (change.elementsToRemove != null)
            {
                foreach (Edge edge in change.elementsToRemove.OfType<Edge>())
                {
                    string id = edge.userData as string;
                    if (!string.IsNullOrWhiteSpace(id) &&
                        (DeleteRequested == null || !DeleteRequested(id)))
                        SceneFlowGraphCommands.Remove(map, id);
                }
            }

            if (!reconnecting && change.edgesToCreate != null)
            {
                foreach (Edge edge in change.edgesToCreate)
                {
                    string source = (edge.output.node as SceneNodeView)?.SceneId;
                    string target = (edge.input.node as SceneNodeView)?.SceneId;
                    if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) continue;
                    string id = SceneFlowGraphCommands.GenerateUniqueConnectionId(map, source, target);
                    SceneFlowGraphCommands.Add(map, id, source, target);
                    edge.userData = id;
                    edge.viewDataKey = id;
                }
            }

            Changed?.Invoke();
            return change;
        }
    }
}
