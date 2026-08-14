using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Interactions;
using QuietStatic.Toolkit.Objectives;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor
{
    /// <summary>Exports existing objective and readable assets to narrative-authorer JSON.</summary>
    public static class NarrativeContentJsonExporter
    {
        [Serializable]
        private sealed class ObjectiveDocument
        {
            public int schemaVersion = 1;
            public string contentType = "objectives";
            public string catalogId;
            public string unityDatabasePath;
            public ObjectiveItem[] items;
        }

        [Serializable]
        private sealed class ObjectiveItem
        {
            public string id;
            public string title;
            public string description;
            public Requirement activationRequirement;
            public Requirement completionRequirement;
            public string unityAssetPath;
        }

        [Serializable]
        private sealed class ReadableDocument
        {
            public int schemaVersion = 1;
            public string contentType = "readables";
            public string catalogId;
            public ReadableItem[] items;
        }

        [Serializable]
        private sealed class ReadableItem
        {
            public string id;
            public string title;
            public string body;
            public string closeLabel;
            public string unityAssetPath;
        }

        [Serializable]
        private sealed class Requirement
        {
            public string mode;
            public string[] flags;
        }

        /// <summary>Validates an objective database for lossless authorer export.</summary>
        public static IReadOnlyList<string> ValidateObjectives(
            ObjectiveDatabase database,
            string catalogId = null)
        {
            var errors = new List<string>();
            if (database == null)
            {
                errors.Add("An ObjectiveDatabase is required.");
                return errors.AsReadOnly();
            }

            string resolvedCatalogId = NarrativeJsonPathUtility.ResolveAssetIdentity(
                database,
                catalogId);
            NarrativeJsonPathUtility.ValidateIdentity(resolvedCatalogId, "catalogId", errors);
            if (NarrativeJsonPathUtility.GetUnityAssetPath(database) == null)
                errors.Add("The ObjectiveDatabase must be saved as an asset below Assets.");
            if (database.Objectives == null || database.Objectives.Count == 0)
            {
                errors.Add("The ObjectiveDatabase must contain at least one objective.");
                return errors.AsReadOnly();
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < database.Objectives.Count; index++)
            {
                ObjectiveDefinition objective = database.Objectives[index];
                if (objective == null)
                {
                    errors.Add($"Objective at index {index} is missing.");
                    continue;
                }

                string id = ReadObjectiveId(objective);
                ValidateItemId(id, $"Objective at index {index}", ids, errors);
                if (NarrativeJsonPathUtility.GetUnityAssetPath(objective) == null)
                    errors.Add($"Objective '{id}' must be saved as an asset below Assets.");
                ValidateRequirement(
                    objective.ActivationRequirement,
                    $"Objective '{id}' activationRequirement",
                    errors);
                ValidateRequirement(
                    objective.CompletionRequirement,
                    $"Objective '{id}' completionRequirement",
                    errors);
            }
            return errors.AsReadOnly();
        }

        /// <summary>Builds version-one objective-catalog JSON in database order.</summary>
        public static string BuildObjectivesJson(
            ObjectiveDatabase database,
            string catalogId = null)
        {
            IReadOnlyList<string> errors = ValidateObjectives(database, catalogId);
            ThrowIfInvalid(errors);

            string unityDatabasePath = NarrativeJsonPathUtility.GetUnityAssetPath(database);
            var document = new ObjectiveDocument
            {
                catalogId = NarrativeJsonPathUtility.ResolveAssetIdentity(database, catalogId),
                unityDatabasePath = unityDatabasePath,
                items = database.Objectives.Select(BuildObjectiveItem).ToArray(),
            };
            return JsonUtility.ToJson(document, true) + Environment.NewLine;
        }

        /// <summary>Exports an objective database to a version-one JSON catalog.</summary>
        public static string ExportObjectives(
            ObjectiveDatabase database,
            string outputPath,
            string catalogId = null) =>
            NarrativeJsonPathUtility.WriteJson(
                outputPath,
                BuildObjectivesJson(database, catalogId));

        /// <summary>Prompts for a destination and exports an objective database.</summary>
        public static string ExportObjectivesWithSavePanel(ObjectiveDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));
            string id = NarrativeJsonPathUtility.ResolveAssetIdentity(database, null);
            string path = EditorUtility.SaveFilePanel(
                "Export Objective Catalog JSON",
                NarrativeJsonPathUtility.GetInitialFolder(database),
                id + ".json",
                "json");
            return string.IsNullOrEmpty(path) ? null : ExportObjectives(database, path);
        }

        /// <summary>Validates readable assets for one authorer catalog.</summary>
        public static IReadOnlyList<string> ValidateReadables(
            IEnumerable<ReadableContentDefinition> definitions,
            string catalogId)
        {
            var errors = new List<string>();
            NarrativeJsonPathUtility.ValidateIdentity(catalogId, "catalogId", errors);
            ReadableContentDefinition[] values = definitions?.ToArray();
            if (values == null || values.Length == 0)
            {
                errors.Add("At least one ReadableContentDefinition is required.");
                return errors.AsReadOnly();
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                ReadableContentDefinition definition = values[index];
                if (definition == null)
                {
                    errors.Add($"Readable at index {index} is missing.");
                    continue;
                }

                string id = NarrativeJsonPathUtility.ResolveAssetIdentity(definition, null);
                ValidateItemId(id, $"Readable at index {index}", ids, errors);
                if (NarrativeJsonPathUtility.GetUnityAssetPath(definition) == null)
                    errors.Add($"Readable '{id}' must be saved as an asset below Assets.");
                if (string.IsNullOrWhiteSpace(definition.Body))
                    errors.Add($"Readable '{id}' must have a non-empty body.");
            }
            return errors.AsReadOnly();
        }

        /// <summary>Builds version-one readable-catalog JSON in supplied order.</summary>
        public static string BuildReadablesJson(
            IEnumerable<ReadableContentDefinition> definitions,
            string catalogId)
        {
            ReadableContentDefinition[] values = definitions?.ToArray();
            IReadOnlyList<string> errors = ValidateReadables(values, catalogId);
            ThrowIfInvalid(errors);
            var document = new ReadableDocument
            {
                catalogId = catalogId,
                items = values.Select(BuildReadableItem).ToArray(),
            };
            return JsonUtility.ToJson(document, true) + Environment.NewLine;
        }

        /// <summary>Exports readable assets to one version-one JSON catalog.</summary>
        public static string ExportReadables(
            IEnumerable<ReadableContentDefinition> definitions,
            string outputPath,
            string catalogId = null)
        {
            string resolvedCatalogId = catalogId ?? Path.GetFileNameWithoutExtension(outputPath);
            return NarrativeJsonPathUtility.WriteJson(
                outputPath,
                BuildReadablesJson(definitions, resolvedCatalogId));
        }

        /// <summary>Prompts for a destination and exports readable assets.</summary>
        public static string ExportReadablesWithSavePanel(
            IEnumerable<ReadableContentDefinition> definitions)
        {
            ReadableContentDefinition[] values = definitions?.ToArray();
            if (values == null || values.Length == 0)
                throw new ArgumentException("At least one readable is required.", nameof(definitions));
            string path = EditorUtility.SaveFilePanel(
                "Export Readable Catalog JSON",
                NarrativeJsonPathUtility.GetInitialFolder(values[0]),
                "readables.json",
                "json");
            return string.IsNullOrEmpty(path) ? null : ExportReadables(values, path);
        }

        [MenuItem("Tools/Quiet Static/Objectives/Export Selected Objective Database JSON...")]
        private static void ExportSelectedObjectives() =>
            RunMenuExport(
                Selection.activeObject,
                () => ExportObjectivesWithSavePanel(Selection.activeObject as ObjectiveDatabase),
                "objective catalog");

        [MenuItem("Tools/Quiet Static/Objectives/Export Selected Objective Database JSON...", true)]
        private static bool CanExportSelectedObjectives() =>
            Selection.activeObject is ObjectiveDatabase;

        [MenuItem("Tools/Quiet Static/Readables/Export Selected Readable Content JSON...")]
        private static void ExportSelectedReadables()
        {
            ReadableContentDefinition[] definitions = Selection.objects
                .OfType<ReadableContentDefinition>()
                .ToArray();
            RunMenuExport(
                definitions.FirstOrDefault(),
                () => ExportReadablesWithSavePanel(definitions),
                "readable catalog");
        }

        [MenuItem("Tools/Quiet Static/Readables/Export Selected Readable Content JSON...", true)]
        private static bool CanExportSelectedReadables() =>
            Selection.objects.Length > 0 &&
            Selection.objects.All(value => value is ReadableContentDefinition);

        private static ObjectiveItem BuildObjectiveItem(ObjectiveDefinition objective)
        {
            string path = NarrativeJsonPathUtility.GetUnityAssetPath(objective);
            var item = new ObjectiveItem { unityAssetPath = path };
            item.id = ReadObjectiveId(objective);
            item.title = objective.Title;
            item.description = objective.Description;
            item.activationRequirement = BuildRequirement(objective.ActivationRequirement);
            item.completionRequirement = BuildRequirement(objective.CompletionRequirement);
            return item;
        }

        private static ReadableItem BuildReadableItem(ReadableContentDefinition definition)
        {
            string path = NarrativeJsonPathUtility.GetUnityAssetPath(definition);
            var item = new ReadableItem { unityAssetPath = path };
            item.id = NarrativeJsonPathUtility.ResolveAssetIdentity(definition, null);
            item.title = definition.Title;
            item.body = definition.Body;
            item.closeLabel = definition.CloseLabel;
            return item;
        }

        private static Requirement BuildRequirement(FlagRequirement requirement) => new()
        {
            mode = (requirement?.Mode ?? FlagRequirementMode.None).ToString(),
            flags = requirement?.Flags.ToArray() ?? Array.Empty<string>(),
        };

        private static string ReadObjectiveId(ObjectiveDefinition objective)
        {
            var serialized = new SerializedObject(objective);
            return serialized.FindProperty("id").stringValue;
        }

        private static void ValidateItemId(
            string id,
            string label,
            ISet<string> ids,
            ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"{label} must have a non-empty ID.");
                return;
            }
            string normalized = id.Trim();
            if (!string.Equals(id, normalized, StringComparison.Ordinal))
                errors.Add($"{label} ID '{id}' must not have surrounding whitespace.");
            if (!ids.Add(normalized))
                errors.Add($"{label} ID '{normalized}' is duplicated.");
        }

        private static void ValidateRequirement(
            FlagRequirement requirement,
            string label,
            ICollection<string> errors)
        {
            FlagRequirementMode mode = requirement?.Mode ?? FlagRequirementMode.None;
            IReadOnlyList<string> flags = requirement?.Flags ?? Array.Empty<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < flags.Count; index++)
            {
                string flag = flags[index];
                if (string.IsNullOrWhiteSpace(flag))
                {
                    errors.Add($"{label}.flags[{index}] must be non-empty.");
                    continue;
                }
                if (!string.Equals(flag, flag.Trim(), StringComparison.Ordinal))
                    errors.Add($"{label}.flags[{index}] must not have surrounding whitespace.");
                if (!seen.Add(flag.Trim()))
                    errors.Add($"{label} contains duplicate flag ID '{flag.Trim()}'.");
            }
            if (mode != FlagRequirementMode.None && flags.Count == 0)
                errors.Add($"{label} needs at least one flag for mode {mode}.");
        }

        private static void ThrowIfInvalid(IReadOnlyList<string> errors)
        {
            if (errors.Count > 0)
                throw new ArgumentException(string.Join(Environment.NewLine, errors));
        }

        private static void RunMenuExport(
            UnityEngine.Object context,
            Func<string> export,
            string label)
        {
            try
            {
                string path = export();
                if (path != null)
                    GameLogger.Log(nameof(NarrativeContentJsonExporter), context,
                        $"Exported {label} to {path}.");
            }
            catch (Exception exception)
            {
                GameLogger.Error(nameof(NarrativeContentJsonExporter), context,
                    $"Narrative export failed: {exception.Message}");
                EditorUtility.DisplayDialog("Narrative Export Failed", exception.Message, "OK");
            }
        }
    }
}
