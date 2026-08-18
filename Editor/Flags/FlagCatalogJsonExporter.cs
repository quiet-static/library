using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Flags;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Flags
{
    /// <summary>
    /// Exports a Unity <see cref="FlagDatabase"/> as a version-one narrative flag catalog.
    /// </summary>
    /// <remarks>
    /// The resulting JSON is compatible with the Quiet Static dialogue authorer and with
    /// <see cref="NarrativeContentJsonImporter"/>. Exporting does not modify the source asset.
    /// </remarks>
    public static class FlagCatalogJsonExporter
    {
        private const int SchemaVersion = 1;
        private const string ContentType = "flags";

        [Serializable]
        private class CatalogDocument
        {
            public int schemaVersion = SchemaVersion;
            public string contentType = ContentType;
            public string catalogId;
            public CatalogItem[] items;
        }

        [Serializable]
        private sealed class ProjectCatalogDocument : CatalogDocument
        {
            public string unityDatabasePath;
        }

        [Serializable]
        private sealed class CatalogItem
        {
            public string id;
            public string description;
        }

        /// <summary>
        /// Validates a database for export without writing a file.
        /// </summary>
        /// <param name="database">Database whose definitions will be exported.</param>
        /// <param name="catalogId">
        /// Optional catalog ID. When omitted, the source asset filename (or object name for an
        /// unsaved object) is used.
        /// </param>
        /// <returns>Validation errors. An empty collection means the database is exportable.</returns>
        public static IReadOnlyList<string> Validate(
            FlagDatabase database,
            string catalogId = null)
        {
            var errors = new List<string>();
            if (database == null)
            {
                errors.Add("A FlagDatabase is required.");
                return errors.AsReadOnly();
            }

            string resolvedCatalogId = NarrativeJsonPathUtility.ResolveAssetIdentity(
                database,
                catalogId);
            NarrativeJsonPathUtility.ValidateIdentity(resolvedCatalogId, "catalogId", errors);

            FlagDatabase.FlagDefinition[] definitions = database.Flags;
            if (definitions == null || definitions.Length == 0)
            {
                errors.Add("The FlagDatabase must contain at least one flag.");
                return errors.AsReadOnly();
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < definitions.Length; index++)
            {
                FlagDatabase.FlagDefinition definition = definitions[index];
                if (definition == null || string.IsNullOrWhiteSpace(definition.id))
                {
                    errors.Add($"Flag at index {index} must have a non-empty ID.");
                    continue;
                }

                string normalizedId = definition.id.Trim();
                if (!string.Equals(definition.id, normalizedId, StringComparison.Ordinal))
                    errors.Add($"Flag ID '{definition.id}' must not have leading or trailing whitespace.");
                if (!ids.Add(normalizedId))
                    errors.Add($"Flag ID '{normalizedId}' is duplicated after trimming.");
            }

            return errors.AsReadOnly();
        }

        /// <summary>
        /// Builds normalized, authorer-compatible JSON without changing the database or filesystem.
        /// </summary>
        /// <param name="database">Database whose definitions will be serialized.</param>
        /// <param name="catalogId">
        /// Optional catalog ID. When omitted, the source asset filename (or object name for an
        /// unsaved object) is used.
        /// </param>
        /// <returns>Pretty-printed UTF-16 managed text ready to save as UTF-8 JSON.</returns>
        /// <exception cref="ArgumentException">The database cannot form a valid catalog.</exception>
        public static string BuildJson(FlagDatabase database, string catalogId = null)
        {
            IReadOnlyList<string> errors = Validate(database, catalogId);
            if (errors.Count > 0)
                throw new ArgumentException(string.Join(Environment.NewLine, errors));

            string unityDatabasePath = NarrativeJsonPathUtility.GetUnityAssetPath(database);
            CatalogDocument document = unityDatabasePath == null
                ? new CatalogDocument()
                : new ProjectCatalogDocument { unityDatabasePath = unityDatabasePath };
            document.catalogId = NarrativeJsonPathUtility.ResolveAssetIdentity(database, catalogId);
            document.items = database.Flags.Select(definition => new CatalogItem
            {
                id = definition.id,
                description = definition.description ?? string.Empty,
            }).ToArray();
            return JsonUtility.ToJson(document, true) + Environment.NewLine;
        }

        /// <summary>
        /// Writes a normalized catalog to a JSON file using an atomic replace operation.
        /// </summary>
        /// <param name="database">Database whose definitions will be exported.</param>
        /// <param name="outputPath">Absolute or project-relative destination ending in .json.</param>
        /// <param name="catalogId">
        /// Optional catalog ID. When omitted, the source asset filename (or object name for an
        /// unsaved object) is used.
        /// </param>
        /// <returns>The absolute path of the exported JSON file.</returns>
        public static string Export(
            FlagDatabase database,
            string outputPath,
            string catalogId = null)
        {
            string json = BuildJson(database, catalogId);
            return NarrativeJsonPathUtility.WriteJson(outputPath, json);
        }

        /// <summary>Prompts for a destination and exports the supplied database.</summary>
        /// <returns>The absolute output path, or <see langword="null"/> when cancelled.</returns>
        public static string ExportWithSavePanel(FlagDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            string initialFolder = NarrativeJsonPathUtility.GetInitialFolder(database);
            string defaultName = NarrativeJsonPathUtility.ResolveAssetIdentity(database, null) + ".json";
            string outputPath = EditorUtility.SaveFilePanel(
                "Export Flag Catalog JSON",
                initialFolder,
                defaultName,
                "json");
            if (string.IsNullOrEmpty(outputPath))
                return null;

            return Export(database, outputPath);
        }

        [MenuItem(QuietStaticMenuPaths.Toolkit + "Flags/Export Selected Flag Database JSON...")]
        private static void ExportSelectedFromMenu()
        {
            var database = Selection.activeObject as FlagDatabase;
            try
            {
                string path = ExportWithSavePanel(database);
                if (path == null)
                    return;

                GameLogger.Log(
                    nameof(FlagCatalogJsonExporter),
                    database,
                    $"Exported flag catalog to {path}.");
                string assetPath = NarrativeJsonPathUtility.TryGetProjectRelativePath(path);
                if (assetPath == null)
                    return;

                TextAsset exported = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (exported != null)
                {
                    Selection.activeObject = exported;
                    EditorGUIUtility.PingObject(exported);
                }
            }
            catch (Exception exception)
            {
                GameLogger.Error(
                    nameof(FlagCatalogJsonExporter),
                    database,
                    $"Flag catalog export failed: {exception.Message}");
                EditorUtility.DisplayDialog("Flag Catalog Export Failed", exception.Message, "OK");
            }
        }

        [MenuItem(QuietStaticMenuPaths.Toolkit + "Flags/Export Selected Flag Database JSON...", true)]
        private static bool CanExportSelectedFromMenu() =>
            Selection.activeObject is FlagDatabase;

    }
}
