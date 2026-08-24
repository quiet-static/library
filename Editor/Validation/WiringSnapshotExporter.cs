using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Validation
{
    /// <summary>Exports normalized, timestamp-free communication wiring JSON.</summary>
    public static class WiringSnapshotExporter
    {
        [Serializable]
        private sealed class Snapshot
        {
            public SnapshotNode[] nodes;
            public SnapshotEdge[] edges;
        }

        [Serializable]
        private sealed class SnapshotNode
        {
            public string id;
            public string label;
            public string kind;
        }

        [Serializable]
        private sealed class SnapshotEdge
        {
            public string sourceId;
            public string targetId;
            public string kind;
            public string propertyPath;
        }

        public static string BuildJson(CommunicationGraph graph)
        {
            graph ??= new CommunicationGraph(
                Array.Empty<CommunicationNode>(),
                Array.Empty<CommunicationEdge>());
            var snapshot = new Snapshot
            {
                nodes = graph.Nodes
                    .OrderBy(node => node.Id, StringComparer.Ordinal)
                    .Select(node => new SnapshotNode
                    {
                        id = node.Id,
                        label = node.Label,
                        kind = node.Kind.ToString(),
                    }).ToArray(),
                edges = graph.Edges
                    .OrderBy(edge => edge.SourceId, StringComparer.Ordinal)
                    .ThenBy(edge => edge.TargetId, StringComparer.Ordinal)
                    .ThenBy(edge => edge.Kind)
                    .ThenBy(edge => edge.PropertyPath, StringComparer.Ordinal)
                    .Select(edge => new SnapshotEdge
                    {
                        sourceId = edge.SourceId,
                        targetId = edge.TargetId,
                        kind = edge.Kind.ToString(),
                        propertyPath = edge.PropertyPath,
                    }).ToArray(),
            };
            return JsonUtility.ToJson(snapshot, true).Replace("\r\n", "\n");
        }

        /// <summary>Batch and menu entry point that exports enabled build-scene wiring.</summary>
        public static void ExportBuildScenes()
        {
            string logsDirectory = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../Logs"));
            Directory.CreateDirectory(logsDirectory);
            string path = Path.Combine(logsDirectory, "WiringSnapshot.json");
            File.WriteAllText(
                path,
                BuildJson(CommunicationGraphExtractor.ScanBuildScenes()));
            Debug.Log($"Wiring snapshot exported to {path}");
        }
    }
}
