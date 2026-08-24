using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.Validation
{
    public enum CommunicationNodeKind
    {
        Component,
        Handler,
        Channel,
        Receiver,
        Service,
        Presenter,
        Diagnostic
    }

    public sealed class CommunicationNode
    {
        public CommunicationNode(string id, string label, CommunicationNodeKind kind, UnityEngine.Object context)
        {
            Id = id;
            Label = label;
            Kind = kind;
            Context = context;
        }

        public string Id { get; }
        public string Label { get; }
        public CommunicationNodeKind Kind { get; }
        public UnityEngine.Object Context { get; }
    }

    public sealed class CommunicationEdge
    {
        public CommunicationEdge(
            string sourceId,
            string targetId,
            string propertyPath,
            CommunicationEdgeKind kind = CommunicationEdgeKind.SerializedReference)
        {
            SourceId = sourceId;
            TargetId = targetId;
            PropertyPath = propertyPath;
            Kind = kind;
        }

        public string SourceId { get; }
        public string TargetId { get; }
        public string PropertyPath { get; }
        public CommunicationEdgeKind Kind { get; }
    }

    public enum CommunicationEdgeKind
    {
        SerializedReference,
        UnityEventListener,
        CSharpEventPublisher,
        Diagnostic
    }

    public sealed class CommunicationGraph
    {
        public CommunicationGraph(
            IReadOnlyList<CommunicationNode> nodes,
            IReadOnlyList<CommunicationEdge> edges)
        {
            Nodes = nodes;
            Edges = edges;
        }

        public IReadOnlyList<CommunicationNode> Nodes { get; }
        public IReadOnlyList<CommunicationEdge> Edges { get; }
    }

    /// <summary>Extracts a deterministic, read-only communication graph from loaded scenes.</summary>
    public static class CommunicationGraphExtractor
    {
        public static CommunicationGraph ScanLoadedScenes()
        {
            var objects = new List<UnityEngine.Object>();
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    objects.AddRange(root.GetComponentsInChildren<Component>(true)
                        .Where(component => component != null));
                }
            }

            return Extract(
                objects,
                ArchitectureValidation.ScanOpenScenes(objects.OfType<Component>()));
        }

        /// <summary>Scans selected scene assets, prefab hierarchies, components, and assets.</summary>
        public static CommunicationGraph ScanSelection()
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            var objects = new List<UnityEngine.Object>();
            try
            {
                foreach (UnityEngine.Object selected in Selection.objects.Where(value => value != null))
                {
                    if (selected is SceneAsset)
                    {
                        string path = AssetDatabase.GetAssetPath(selected);
                        Scene scene = SceneManager.GetSceneByPath(path);
                        if (!scene.isLoaded)
                        {
                            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        }
                        CollectScene(scene, objects);
                    }
                    else if (selected is GameObject gameObject)
                    {
                        objects.AddRange(gameObject.GetComponentsInChildren<Component>(true)
                            .Where(component => component != null));
                    }
                    else
                    {
                        objects.Add(selected);
                    }
                }

                return Extract(
                    objects.Distinct(),
                    ArchitectureValidation.ScanOpenScenes(objects.OfType<Component>()));
            }
            finally
            {
                RestoreSceneSetup(previousSetup);
            }
        }

        /// <summary>Loads enabled build scenes read-only, extracts their graph, and restores setup.</summary>
        public static CommunicationGraph ScanBuildScenes()
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            var objects = new List<UnityEngine.Object>();
            try
            {
                string[] paths = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => AssetDatabase.GUIDToAssetPath(scene.guid))
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct()
                    .ToArray();
                for (int index = 0; index < paths.Length; index++)
                {
                    Scene scene = EditorSceneManager.OpenScene(
                        paths[index],
                        index == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
                    CollectScene(scene, objects);
                }
                return Extract(
                    objects,
                    ArchitectureValidation.ScanOpenScenes(objects.OfType<Component>()));
            }
            finally
            {
                RestoreSceneSetup(previousSetup);
            }
        }

        public static CommunicationGraph Extract(
            IEnumerable<UnityEngine.Object> sources,
            IEnumerable<ValidationIssue> issues = null)
        {
            var nodes = new Dictionary<string, CommunicationNode>(StringComparer.Ordinal);
            var edges = new List<CommunicationEdge>();
            foreach (UnityEngine.Object source in sources.Where(value => value != null))
            {
                string sourceId = GetStableId(source);
                nodes[sourceId] = CreateNode(sourceId, source);
                var serialized = new SerializedObject(source);
                SerializedProperty property = serialized.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference ||
                        property.objectReferenceValue == null ||
                        property.propertyPath == "m_Script")
                    {
                        continue;
                    }

                    UnityEngine.Object target = property.objectReferenceValue;
                    string targetId = GetStableId(target);
                    nodes[targetId] = CreateNode(targetId, target);
                    CommunicationEdgeKind kind = property.propertyPath.Contains("m_PersistentCalls")
                        ? CommunicationEdgeKind.UnityEventListener
                        : CommunicationEdgeKind.SerializedReference;
                    edges.Add(new CommunicationEdge(sourceId, targetId, property.propertyPath, kind));
                }

                foreach (EventInfo eventInfo in source.GetType().GetEvents(
                             BindingFlags.Instance |
                             BindingFlags.Public |
                             BindingFlags.DeclaredOnly))
                {
                    edges.Add(new CommunicationEdge(
                        sourceId,
                        sourceId,
                        $"event:{eventInfo.Name}",
                        CommunicationEdgeKind.CSharpEventPublisher));
                }
            }

            int diagnosticIndex = 0;
            foreach (ValidationIssue issue in issues ?? Array.Empty<ValidationIssue>())
            {
                string id = $"diagnostic:{issue.Code}:{diagnosticIndex++:D4}";
                nodes[id] = new CommunicationNode(
                    id,
                    $"[{issue.Code}] {issue.Message}",
                    CommunicationNodeKind.Diagnostic,
                    issue.Context);
                if (issue.Context != null)
                {
                    string targetId = GetStableId(issue.Context);
                    nodes[targetId] = CreateNode(targetId, issue.Context);
                    edges.Add(new CommunicationEdge(
                        id,
                        targetId,
                        "diagnostic-context",
                        CommunicationEdgeKind.Diagnostic));
                }
            }

            return new CommunicationGraph(
                nodes.Values.OrderBy(node => node.Id, StringComparer.Ordinal).ToArray(),
                edges.OrderBy(edge => edge.SourceId, StringComparer.Ordinal)
                    .ThenBy(edge => edge.TargetId, StringComparer.Ordinal)
                    .ThenBy(edge => edge.PropertyPath, StringComparer.Ordinal)
                    .ToArray());
        }

        private static void CollectScene(Scene scene, ICollection<UnityEngine.Object> objects)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component != null) objects.Add(component);
                }
            }
        }

        private static void RestoreSceneSetup(SceneSetup[] setup)
        {
            if (setup != null && setup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            else
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static CommunicationNode CreateNode(string id, UnityEngine.Object value)
        {
            string typeName = value.GetType().Name;
            CommunicationNodeKind kind = typeName.EndsWith("Channel", StringComparison.Ordinal)
                ? CommunicationNodeKind.Channel
                : typeName.EndsWith("Listener", StringComparison.Ordinal) ||
                  typeName.EndsWith("Receiver", StringComparison.Ordinal)
                    ? CommunicationNodeKind.Receiver
                    : typeName.EndsWith("Handler", StringComparison.Ordinal)
                        ? CommunicationNodeKind.Handler
                        : typeName.EndsWith("Manager", StringComparison.Ordinal)
                            ? CommunicationNodeKind.Service
                            : typeName.EndsWith("Presenter", StringComparison.Ordinal) ||
                              typeName.EndsWith("View", StringComparison.Ordinal)
                                ? CommunicationNodeKind.Presenter
                                : CommunicationNodeKind.Component;
            return new CommunicationNode(id, $"{value.name} ({typeName})", kind, value);
        }

        private static string GetStableId(UnityEngine.Object value)
        {
            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(value);
            string text = globalId.ToString();
            return globalId.identifierType != 0
                ? text
                : $"transient:{value.GetType().FullName}:{value.GetInstanceID()}";
        }
    }
}
