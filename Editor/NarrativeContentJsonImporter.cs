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
    /// <summary>Imports versioned narrative content catalogs into the existing runtime ScriptableObjects.</summary>
    public static class NarrativeContentJsonImporter
    {
        private const string DefaultOutputFolder = "Assets/Generated/Narrative";
        private const string ObjectiveImportModePreserve = "Preserve";
        private const string ObjectiveImportModeReplace = "Replace";

        internal enum ImportTargetIntent
        {
            CreateOrUpdate,
            Regenerate,
            Delete,
        }

        internal sealed class ImportTarget
        {
            public ImportTarget(
                string assetPath,
                Type assetType,
                string contentId,
                ImportTargetIntent intent = ImportTargetIntent.CreateOrUpdate)
            {
                AssetPath = assetPath;
                AssetType = assetType;
                ContentId = contentId;
                Intent = intent;
            }

            public string AssetPath { get; }
            public Type AssetType { get; }
            public string ContentId { get; }
            public ImportTargetIntent Intent { get; }
        }

        [Serializable] private sealed class Document
        {
            public int schemaVersion;
            public string contentType;
            public string catalogId;
            public string unityDatabasePath;
            public string unityObjectiveImportMode;
            public Item[] items;
        }
        [Serializable] private sealed class Item
        {
            public string id; public string title; public string description; public string body;
            public string unityAssetPath;
            public string closeLabel = "Close"; public Requirement activationRequirement; public Requirement completionRequirement;
        }
        [Serializable] private sealed class Requirement { public string mode = "None"; public string[] flags; }

        private sealed class ExistingObjective
        {
            public ObjectiveDefinition Definition;
            public string Id;
            public string Path;
            public string Guid;
        }

        private sealed class ObjectiveReplacementPlan
        {
            public ObjectiveDatabase Database;
            public string DatabasePath;
            public string DefinitionsFolder;
            public ExistingObjective[] ExistingObjectives;
            public string[] DefinitionPaths;
        }

        private sealed class AssetMove
        {
            public string From;
            public string To;
            public string Guid;
        }

        /// <summary>Imports the selected content-catalog JSON.</summary>
        [MenuItem(QuietStaticMenuPaths.Toolkit + "Importers/Import Selected Content JSON")]
        private static void ImportSelected()
        {
            try
            {
                UnityEngine.Object result = Import((TextAsset)Selection.activeObject);
                Selection.activeObject = result;
                EditorGUIUtility.PingObject(result);
            }
            catch (Exception exception)
            {
                GameLogger.Error(nameof(NarrativeContentJsonImporter), Selection.activeObject,
                    $"Narrative content import failed: {exception.Message}");
            }
        }

        [MenuItem(QuietStaticMenuPaths.Toolkit + "Importers/Import Selected Content JSON", true)]
        private static bool CanImportSelected() => Selection.activeObject is TextAsset asset &&
            AssetDatabase.GetAssetPath(asset).EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        /// <summary>Validates and imports a flag, objective, or readable content catalog.</summary>
        public static UnityEngine.Object Import(TextAsset source, string outputFolder = DefaultOutputFolder)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!NarrativeJsonPathUtility.IsCanonicalAssetFolder(outputFolder))
                throw new ArgumentException("Output folder must be under Assets.", nameof(outputFolder));
            string sourcePath = AssetDatabase.GetAssetPath(source);
            Document document = ParseAndValidate(
                source.text,
                sourcePath);
            string folder = $"{outputFolder.TrimEnd('/')}/{Safe(document.catalogId)}";
            ObjectiveReplacementPlan replacementPlan = IsObjectiveReplacement(document)
                ? BuildObjectiveReplacementPlan(document, folder, sourcePath)
                : null;
            BuildImportTargets(document, folder, sourcePath, replacementPlan);
            UnityEngine.Object result = document.contentType switch
            {
                "flags" => ImportFlags(document, folder),
                "objectives" => replacementPlan == null
                    ? ImportObjectivesPreservingAssets(document, folder)
                    : ImportObjectivesReplacingAssets(document, replacementPlan),
                "readables" => ImportReadables(document, folder),
                _ => throw new ArgumentException($"Unsupported contentType '{document.contentType}'.")
            };
            AssetDatabase.SaveAssets();
            return result;
        }

        /// <summary>Fully validates narrative content JSON without changing assets.</summary>
        /// <param name="json">Flag, objective, or readable catalog JSON.</param>
        /// <param name="sourcePath">Path or label included in validation messages.</param>
        public static void ValidateJson(string json, string sourcePath = "<input>") =>
            ParseAndValidate(json, sourcePath);

        /// <summary>
        /// Validates narrative JSON and any existing Unity assets that a replacement import
        /// would remove, without changing the project.
        /// </summary>
        /// <param name="json">Flag, objective, or readable catalog JSON.</param>
        /// <param name="outputFolder">Assets folder used to resolve generated targets.</param>
        /// <param name="sourcePath">Path or label included in validation messages.</param>
        public static void PreflightImport(
            string json,
            string outputFolder = DefaultOutputFolder,
            string sourcePath = "<input>")
        {
            GetImportTargets(json, outputFolder, sourcePath);
        }

        /// <summary>
        /// Resolves every asset affected by an import while performing the same validation used
        /// by the writer. This remains internal so batch preflight can present an exact preview
        /// without duplicating content-import path or replacement ownership rules.
        /// </summary>
        internal static IReadOnlyList<ImportTarget> GetImportTargets(
            string json,
            string outputFolder = DefaultOutputFolder,
            string sourcePath = "<input>")
        {
            if (!NarrativeJsonPathUtility.IsCanonicalAssetFolder(outputFolder))
                throw new ArgumentException("Output folder must be under Assets.", nameof(outputFolder));

            Document document = ParseAndValidate(json, sourcePath);
            string folder = $"{outputFolder.TrimEnd('/')}/{Safe(document.catalogId)}";
            ObjectiveReplacementPlan replacementPlan = IsObjectiveReplacement(document)
                ? BuildObjectiveReplacementPlan(document, folder, sourcePath)
                : null;
            return BuildImportTargets(
                document,
                folder,
                sourcePath,
                replacementPlan);
        }

        private static IReadOnlyList<ImportTarget> BuildImportTargets(
            Document document,
            string folder,
            string sourcePath,
            ObjectiveReplacementPlan replacementPlan)
        {
            var targets = new List<ImportTarget>();
            switch (document.contentType)
            {
                case "flags":
                    targets.Add(new ImportTarget(
                        document.unityDatabasePath ??
                        $"{folder}/{Safe(document.catalogId)}.asset",
                        typeof(FlagDatabase),
                        document.catalogId));
                    break;

                case "objectives" when replacementPlan != null:
                    AddObjectiveReplacementTargets(
                        document,
                        replacementPlan,
                        targets);
                    break;

                case "objectives":
                    targets.Add(new ImportTarget(
                        document.unityDatabasePath ??
                        $"{folder}/{Safe(document.catalogId)}.asset",
                        typeof(ObjectiveDatabase),
                        document.catalogId));
                    foreach (Item item in document.items)
                    {
                        targets.Add(new ImportTarget(
                            item.unityAssetPath ??
                            $"{folder}/{Safe(item.id)}.asset",
                            typeof(ObjectiveDefinition),
                            item.id));
                    }
                    break;

                case "readables":
                    foreach (Item item in document.items)
                    {
                        targets.Add(new ImportTarget(
                            item.unityAssetPath ??
                            $"{folder}/{Safe(item.id)}.asset",
                            typeof(ReadableContentDefinition),
                            item.id));
                    }
                    break;
            }
            ValidateImportTargets(targets, sourcePath);
            return targets.AsReadOnly();
        }

        private static void AddObjectiveReplacementTargets(
            Document document,
            ObjectiveReplacementPlan plan,
            ICollection<ImportTarget> targets)
        {
            targets.Add(new ImportTarget(
                plan.DatabasePath,
                typeof(ObjectiveDatabase),
                document.catalogId));

            var existingByPath = plan.ExistingObjectives.ToDictionary(
                objective => objective.Path,
                objective => objective,
                StringComparer.OrdinalIgnoreCase);
            var generatedPaths = new HashSet<string>(
                plan.DefinitionPaths,
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < document.items.Length; index++)
            {
                string definitionPath = plan.DefinitionPaths[index];
                targets.Add(new ImportTarget(
                    definitionPath,
                    typeof(ObjectiveDefinition),
                    document.items[index].id,
                    existingByPath.ContainsKey(definitionPath)
                        ? ImportTargetIntent.Regenerate
                        : ImportTargetIntent.CreateOrUpdate));
            }

            foreach (ExistingObjective existing in plan.ExistingObjectives)
            {
                if (generatedPaths.Contains(existing.Path))
                    continue;
                targets.Add(new ImportTarget(
                    existing.Path,
                    typeof(ObjectiveDefinition),
                    existing.Id,
                    ImportTargetIntent.Delete));
            }
        }

        private static void ValidateImportTargets(
            IEnumerable<ImportTarget> targets,
            string sourcePath)
        {
            var claimedPaths = new Dictionary<string, ImportTarget>(
                StringComparer.OrdinalIgnoreCase);
            foreach (ImportTarget target in targets)
            {
                var errors = new List<string>();
                NarrativeJsonPathUtility.ValidateUnityAssetPath(
                    target.AssetPath,
                    target.AssetType,
                    $"{sourcePath}: target for '{target.ContentId}'",
                    errors);
                if (errors.Count > 0)
                    throw new ArgumentException(string.Join(Environment.NewLine, errors));

                if (claimedPaths.TryGetValue(
                        target.AssetPath,
                        out ImportTarget existingTarget))
                {
                    throw new ArgumentException(
                        $"{sourcePath}: targets '{existingTarget.ContentId}' " +
                        $"({existingTarget.AssetType.Name}) and '{target.ContentId}' " +
                        $"({target.AssetType.Name}) resolve to the same Unity asset path: " +
                        target.AssetPath);
                }
                claimedPaths.Add(target.AssetPath, target);
            }
        }

        private static Document ParseAndValidate(string json, string sourcePath)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            sourcePath = string.IsNullOrWhiteSpace(sourcePath) ? "<input>" : sourcePath;
            Document document;
            try { document = JsonUtility.FromJson<Document>(json); }
            catch (Exception exception)
            {
                throw new ArgumentException(
                    $"{sourcePath}: invalid JSON: {exception.Message}",
                    exception);
            }
            Validate(document, sourcePath);
            return document;
        }

        private static FlagDatabase ImportFlags(Document document, string folder)
        {
            string path = document.unityDatabasePath ??
                          $"{folder}/{Safe(document.catalogId)}.asset";
            NarrativeJsonPathUtility.EnsureAssetFolderForPath(path);
            FlagDatabase database = LoadOrCreate<FlagDatabase>(path);
            SerializedObject serialized = new(database);
            SerializedProperty flags = serialized.FindProperty("flags");
            flags.arraySize = document.items.Length;
            for (int index = 0; index < document.items.Length; index++)
            {
                SerializedProperty flag = flags.GetArrayElementAtIndex(index);
                flag.FindPropertyRelative("id").stringValue = document.items[index].id.Trim();
                flag.FindPropertyRelative("description").stringValue = document.items[index].description ?? string.Empty;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(database); return database;
        }

        private static ObjectiveDatabase ImportObjectivesPreservingAssets(Document document, string folder)
        {
            var definitions = new ObjectiveDefinition[document.items.Length];
            for (int index = 0; index < document.items.Length; index++)
            {
                Item item = document.items[index];
                string definitionPath = item.unityAssetPath ??
                                        $"{folder}/{Safe(item.id)}.asset";
                NarrativeJsonPathUtility.EnsureAssetFolderForPath(definitionPath);
                ObjectiveDefinition definition = LoadOrCreate<ObjectiveDefinition>(definitionPath);
                WriteObjective(definition, item);
                definitions[index] = definition;
            }
            string databasePath = document.unityDatabasePath ??
                                  $"{folder}/{Safe(document.catalogId)}.asset";
            NarrativeJsonPathUtility.EnsureAssetFolderForPath(databasePath);
            ObjectiveDatabase database = LoadOrCreate<ObjectiveDatabase>(databasePath);
            WriteObjectiveDatabase(database, definitions);
            return database;
        }

        private static ObjectiveDatabase ImportObjectivesReplacingAssets(
            Document document,
            ObjectiveReplacementPlan plan)
        {
            string transactionRoot =
                $"{Path.GetDirectoryName(plan.DefinitionsFolder)?.Replace('\\', '/')}/" +
                $"__ObjectiveImport_{Guid.NewGuid():N}";
            string stagingFolder = transactionRoot + "/Staging";
            string backupFolder = transactionRoot + "/Backup";
            var stagedPaths = new List<string>(document.items.Length);
            var movedNewAssets = new List<AssetMove>(document.items.Length);
            var movedOldAssets = new List<AssetMove>(plan.ExistingObjectives.Length);
            ObjectiveDatabase database = plan.Database;
            bool databaseCreated = false;
            bool databaseSwapped = false;

            try
            {
                EnsureFolder(stagingFolder);
                EnsureFolder(backupFolder);
                for (int index = 0; index < document.items.Length; index++)
                {
                    string stagingPath =
                        $"{stagingFolder}/{index:D4}_{Safe(document.items[index].id)}.asset";
                    ObjectiveDefinition definition = ScriptableObject.CreateInstance<ObjectiveDefinition>();
                    AssetDatabase.CreateAsset(definition, stagingPath);
                    WriteObjective(definition, document.items[index]);
                    stagedPaths.Add(stagingPath);
                }

                for (int index = 0; index < plan.ExistingObjectives.Length; index++)
                {
                    ExistingObjective existing = plan.ExistingObjectives[index];
                    string backupPath =
                        $"{backupFolder}/{index:D4}_{Path.GetFileName(existing.Path)}";
                    MoveAssetOrThrow(existing.Path, backupPath);
                    movedOldAssets.Add(new AssetMove
                    {
                        From = existing.Path,
                        To = backupPath,
                        Guid = existing.Guid,
                    });
                }

                EnsureFolder(plan.DefinitionsFolder);
                var definitions = new ObjectiveDefinition[document.items.Length];
                for (int index = 0; index < stagedPaths.Count; index++)
                {
                    string finalPath = plan.DefinitionPaths[index];
                    MoveAssetOrThrow(stagedPaths[index], finalPath);
                    movedNewAssets.Add(new AssetMove { From = stagedPaths[index], To = finalPath });
                    definitions[index] = AssetDatabase.LoadAssetAtPath<ObjectiveDefinition>(finalPath);
                    if (definitions[index] == null)
                        throw new InvalidOperationException(
                            $"Unity did not load the regenerated objective at '{finalPath}'.");
                }

                NarrativeJsonPathUtility.EnsureAssetFolderForPath(plan.DatabasePath);
                if (database == null)
                {
                    database = ScriptableObject.CreateInstance<ObjectiveDatabase>();
                    AssetDatabase.CreateAsset(database, plan.DatabasePath);
                    databaseCreated = true;
                }
                WriteObjectiveDatabase(database, definitions);
                databaseSwapped = true;
                AssetDatabase.SaveAssets();
            }
            catch
            {
                if (!databaseSwapped)
                {
                    if (databaseCreated)
                        AssetDatabase.DeleteAsset(plan.DatabasePath);
                    RollBackObjectiveReplacement(transactionRoot, movedNewAssets, movedOldAssets);
                }
                throw;
            }

            bool cleanupSucceeded = true;
            foreach (AssetMove oldAsset in movedOldAssets)
            {
                string currentPath = AssetDatabase.GUIDToAssetPath(oldAsset.Guid);
                if (string.IsNullOrEmpty(currentPath))
                    continue;
                bool pathMatches = string.Equals(
                        AssetDatabase.AssetPathToGUID(currentPath),
                        oldAsset.Guid,
                        StringComparison.OrdinalIgnoreCase);
                bool deleted = pathMatches && AssetDatabase.DeleteAsset(currentPath);
                if (!deleted)
                    cleanupSucceeded = false;
            }
            if (!AssetDatabase.DeleteAsset(transactionRoot))
                cleanupSucceeded = false;
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            if (!cleanupSucceeded)
            {
                GameLogger.Warning(
                    nameof(NarrativeContentJsonImporter),
                    database,
                    $"Objectives were regenerated, but Unity could not remove temporary backup assets below '{transactionRoot}'.");
            }
            return database;
        }

        private static ReadableContentDefinition ImportReadables(Document document, string folder)
        {
            ReadableContentDefinition first = null;
            foreach (Item item in document.items)
            {
                string definitionPath = item.unityAssetPath ??
                                        $"{folder}/{Safe(item.id)}.asset";
                NarrativeJsonPathUtility.EnsureAssetFolderForPath(definitionPath);
                ReadableContentDefinition definition = LoadOrCreate<ReadableContentDefinition>(definitionPath);
                SerializedObject serialized = new(definition);
                serialized.FindProperty("title").stringValue = item.title ?? string.Empty;
                serialized.FindProperty("body").stringValue = item.body ?? string.Empty;
                serialized.FindProperty("closeLabel").stringValue = item.closeLabel ?? "Close";
                serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(definition); first ??= definition;
            }
            return first;
        }

        private static void Validate(Document document, string source)
        {
            var errors = new List<string>();
            if (document == null) throw new ArgumentException($"{source}: top-level value must be an object.");
            if (document.schemaVersion != 1) errors.Add("schemaVersion must be 1.");
            if (!new[] { "flags", "objectives", "readables" }.Contains(document.contentType)) errors.Add("contentType must be flags, objectives, or readables.");
            if (string.IsNullOrWhiteSpace(document.catalogId)) errors.Add("catalogId must be non-empty.");
            else if (!string.Equals(document.catalogId, document.catalogId.Trim(), StringComparison.Ordinal)) errors.Add("catalogId must not have surrounding whitespace.");
            if (document.items == null || document.items.Length == 0) errors.Add("items must contain at least one item.");
            if (document.unityObjectiveImportMode != null)
            {
                if (document.contentType != "objectives")
                {
                    errors.Add("unityObjectiveImportMode is only valid for objective catalogs.");
                }
                else if (document.unityObjectiveImportMode != ObjectiveImportModePreserve &&
                         document.unityObjectiveImportMode != ObjectiveImportModeReplace)
                {
                    errors.Add(
                        $"unityObjectiveImportMode must be '{ObjectiveImportModePreserve}' or " +
                        $"'{ObjectiveImportModeReplace}'.");
                }
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Item item in document.items ?? Array.Empty<Item>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id)) errors.Add("Every item must have a non-empty id.");
                else
                {
                    string id = item.id.Trim();
                    if (!string.Equals(item.id, id, StringComparison.Ordinal)) errors.Add($"Item id '{item.id}' must not have surrounding whitespace.");
                    if (!ids.Add(id)) errors.Add($"Item id '{item.id}' is duplicated.");
                }
                if (document.contentType == "readables" && string.IsNullOrWhiteSpace(item?.body)) errors.Add($"Readable '{item?.id}' must have a body.");
                if (document.contentType == "objectives")
                {
                    ValidateRequirement(item?.activationRequirement, item?.id, "activation", errors);
                    ValidateRequirement(item?.completionRequirement, item?.id, "completion", errors);
                }
            }
            ValidateUnityPaths(document, errors);
            if (errors.Count > 0) throw new ArgumentException($"{source}: {string.Join(Environment.NewLine, errors)}");
        }

        private static void ValidateRequirement(
            Requirement requirement,
            string id,
            string label,
            ICollection<string> errors)
        {
            if (requirement == null) return;
            if (!Enum.TryParse(requirement.mode, true, out FlagRequirementMode mode) ||
                !Enum.IsDefined(typeof(FlagRequirementMode), mode))
            {
                errors.Add($"Objective '{id}' has an invalid {label} requirement mode.");
                return;
            }
            if (mode != FlagRequirementMode.None && (requirement.flags == null || requirement.flags.Length == 0)) errors.Add($"Objective '{id}' {label} requirement needs flags.");
            if (requirement.flags == null) return;
            if (requirement.flags.Any(string.IsNullOrWhiteSpace)) errors.Add($"Objective '{id}' {label} requirement contains an empty flag.");
            if (requirement.flags.Any(flag => flag != null && !string.Equals(flag, flag.Trim(), StringComparison.Ordinal))) errors.Add($"Objective '{id}' {label} requirement contains surrounding whitespace.");
            if (requirement.flags.Where(flag => flag != null).Select(flag => flag.Trim()).Distinct(StringComparer.Ordinal).Count() != requirement.flags.Length) errors.Add($"Objective '{id}' {label} requirement contains duplicate flags.");
        }

        private static void ValidateUnityPaths(Document document, ICollection<string> errors)
        {
            Type databaseType = document.contentType == "flags"
                ? typeof(FlagDatabase)
                : document.contentType == "objectives"
                    ? typeof(ObjectiveDatabase)
                    : null;
            if (databaseType == null && document.unityDatabasePath != null)
                errors.Add("unityDatabasePath is only valid for flags or objectives.");
            else if (databaseType != null)
                NarrativeJsonPathUtility.ValidateUnityAssetPath(
                    document.unityDatabasePath,
                    databaseType,
                    "unityDatabasePath",
                    errors);

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (document.unityDatabasePath != null)
                paths.Add(document.unityDatabasePath);
            for (int index = 0; index < (document.items?.Length ?? 0); index++)
            {
                Item item = document.items[index];
                if (item?.unityAssetPath == null)
                    continue;
                if (IsObjectiveReplacement(document))
                {
                    errors.Add(
                        $"items[{index}].unityAssetPath cannot be used when " +
                        $"unityObjectiveImportMode is '{ObjectiveImportModeReplace}'.");
                    continue;
                }
                if (document.contentType == "flags")
                {
                    errors.Add($"items[{index}].unityAssetPath is not valid for a flag item.");
                    continue;
                }
                Type itemType = document.contentType == "objectives"
                    ? typeof(ObjectiveDefinition)
                    : typeof(ReadableContentDefinition);
                NarrativeJsonPathUtility.ValidateUnityAssetPath(
                    item.unityAssetPath,
                    itemType,
                    $"items[{index}].unityAssetPath",
                    errors);
                if (!paths.Add(item.unityAssetPath))
                    errors.Add($"items[{index}].unityAssetPath duplicates another Unity asset path.");
            }
        }

        private static bool IsObjectiveReplacement(Document document) =>
            document != null &&
            document.contentType == "objectives" &&
            document.unityObjectiveImportMode == ObjectiveImportModeReplace;

        private static ObjectiveReplacementPlan BuildObjectiveReplacementPlan(
            Document document,
            string folder,
            string sourcePath)
        {
            string databasePath = document.unityDatabasePath ??
                                  $"{folder}/{Safe(document.catalogId)}.asset";
            UnityEngine.Object databaseAsset = AssetDatabase.LoadMainAssetAtPath(databasePath);
            if (databaseAsset != null && databaseAsset is not ObjectiveDatabase)
            {
                throw new ArgumentException(
                    $"{sourcePath}: objective database target contains " +
                    $"{databaseAsset.GetType().Name}, not ObjectiveDatabase: {databasePath}");
            }
            ObjectiveDatabase database = databaseAsset as ObjectiveDatabase;

            var existingObjectives = new List<ExistingObjective>();
            var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (database != null)
            {
                foreach (ObjectiveDefinition definition in database.Objectives)
                {
                    if (definition == null)
                        continue;
                    string path = AssetDatabase.GetAssetPath(definition);
                    if (!NarrativeJsonPathUtility.IsCanonicalAssetPath(path))
                    {
                        throw new ArgumentException(
                            $"{sourcePath}: objective '{definition.Id}' cannot be replaced " +
                            "because it is not a saved .asset below Assets.");
                    }
                    if (AssetDatabase.LoadMainAssetAtPath(path) != definition)
                    {
                        throw new ArgumentException(
                            $"{sourcePath}: objective '{definition.Id}' cannot be replaced " +
                            "because it is not the main asset at its Unity path.");
                    }
                    if (!existingPaths.Add(path))
                        continue;
                    existingObjectives.Add(new ExistingObjective
                    {
                        Definition = definition,
                        Id = definition.Id,
                        Path = path,
                        Guid = AssetDatabase.AssetPathToGUID(path),
                    });
                }
            }

            string definitionsFolder = folder + "/Definitions";
            var definitionPaths = new string[document.items.Length];
            var generatedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < document.items.Length; index++)
            {
                Item item = document.items[index];
                string path = $"{definitionsFolder}/{Safe(item.id)}.asset";
                if (generatedPaths.TryGetValue(path, out string priorId))
                {
                    throw new ArgumentException(
                        $"{sourcePath}: objective IDs '{priorId}' and '{item.id}' map to " +
                        $"the same generated asset path: {path}");
                }
                generatedPaths.Add(path, item.id);
                definitionPaths[index] = path;

                UnityEngine.Object existingTarget = AssetDatabase.LoadMainAssetAtPath(path);
                if (existingTarget != null && !existingPaths.Contains(path))
                {
                    throw new ArgumentException(
                        $"{sourcePath}: cannot replace objectives because generated target " +
                        $"'{path}' contains an asset that is not owned by '{databasePath}'.");
                }
                if (existingTarget != null && existingTarget is not ObjectiveDefinition)
                {
                    throw new ArgumentException(
                        $"{sourcePath}: generated objective target contains " +
                        $"{existingTarget.GetType().Name}, not ObjectiveDefinition: {path}");
                }
            }

            ValidateNoExternalObjectiveReferences(
                existingObjectives,
                databasePath,
                sourcePath);
            return new ObjectiveReplacementPlan
            {
                Database = database,
                DatabasePath = databasePath,
                DefinitionsFolder = definitionsFolder,
                ExistingObjectives = existingObjectives.ToArray(),
                DefinitionPaths = definitionPaths,
            };
        }

        private static void ValidateNoExternalObjectiveReferences(
            IReadOnlyList<ExistingObjective> existingObjectives,
            string databasePath,
            string sourcePath)
        {
            if (existingObjectives.Count == 0)
                return;

            var ignoredPaths = new HashSet<string>(
                existingObjectives.Select(value => value.Path),
                StringComparer.OrdinalIgnoreCase)
            {
                databasePath,
            };
            var referencers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in AssetDatabase.GetAllAssetPaths())
            {
                if (!candidate.StartsWith("Assets/", StringComparison.Ordinal) ||
                    ignoredPaths.Contains(candidate) ||
                    AssetDatabase.IsValidFolder(candidate))
                    continue;
                string[] dependencies = AssetDatabase.GetDependencies(candidate, false);
                foreach (ExistingObjective objective in existingObjectives)
                {
                    if (!dependencies.Contains(objective.Path, StringComparer.OrdinalIgnoreCase))
                        continue;
                    if (!referencers.TryGetValue(objective.Path, out List<string> values))
                    {
                        values = new List<string>();
                        referencers.Add(objective.Path, values);
                    }
                    values.Add(candidate);
                }
            }

            if (referencers.Count == 0)
                return;
            var errors = new List<string>();
            foreach (ExistingObjective objective in existingObjectives)
            {
                if (!referencers.TryGetValue(objective.Path, out List<string> values))
                    continue;
                errors.Add(
                    $"Objective '{objective.Id}' at '{objective.Path}' is referenced by " +
                    string.Join(", ", values.OrderBy(value => value, StringComparer.Ordinal)) + ".");
            }
            throw new ArgumentException(
                $"{sourcePath}: objective replacement would break direct asset references:" +
                Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        private static void WriteObjective(ObjectiveDefinition definition, Item item)
        {
            SerializedObject serialized = new(definition);
            serialized.FindProperty("id").stringValue = item.id.Trim();
            serialized.FindProperty("title").stringValue = item.title ?? string.Empty;
            serialized.FindProperty("description").stringValue = item.description ?? string.Empty;
            WriteRequirement(
                serialized.FindProperty("activationRequirement"),
                item.activationRequirement);
            WriteRequirement(
                serialized.FindProperty("completionRequirement"),
                item.completionRequirement);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void WriteObjectiveDatabase(
            ObjectiveDatabase database,
            IReadOnlyList<ObjectiveDefinition> definitions)
        {
            SerializedObject databaseObject = new(database);
            SerializedProperty objectives = databaseObject.FindProperty("objectives");
            objectives.arraySize = definitions.Count;
            for (int index = 0; index < definitions.Count; index++)
                objectives.GetArrayElementAtIndex(index).objectReferenceValue = definitions[index];
            databaseObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        private static void MoveAssetOrThrow(string from, string to)
        {
            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(
                    $"Could not move narrative asset from '{from}' to '{to}': {error}");
        }

        private static void RollBackObjectiveReplacement(
            string transactionRoot,
            IReadOnlyList<AssetMove> movedNewAssets,
            IReadOnlyList<AssetMove> movedOldAssets)
        {
            bool rollbackSucceeded = true;
            for (int index = movedNewAssets.Count - 1; index >= 0; index--)
            {
                AssetMove move = movedNewAssets[index];
                string error = AssetDatabase.MoveAsset(move.To, move.From);
                if (string.IsNullOrEmpty(error))
                    continue;
                rollbackSucceeded = false;
                UnityEngine.Debug.LogError(
                    $"Objective import rollback could not move '{move.To}' back to " +
                    $"'{move.From}': {error}");
            }
            for (int index = movedOldAssets.Count - 1; index >= 0; index--)
            {
                AssetMove move = movedOldAssets[index];
                string error = AssetDatabase.MoveAsset(move.To, move.From);
                if (string.IsNullOrEmpty(error))
                    continue;
                rollbackSucceeded = false;
                UnityEngine.Debug.LogError(
                    $"Objective import rollback could not restore '{move.From}' from " +
                    $"'{move.To}': {error}");
            }
            if (rollbackSucceeded)
                AssetDatabase.DeleteAsset(transactionRoot);
        }

        private static void WriteRequirement(SerializedProperty property, Requirement requirement)
        {
            requirement ??= new Requirement(); Enum.TryParse(requirement.mode, true, out FlagRequirementMode mode);
            property.FindPropertyRelative("mode").enumValueIndex = (int)mode;
            SerializedProperty flags = property.FindPropertyRelative("flags"); string[] values = requirement.flags ?? Array.Empty<string>(); flags.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++) flags.GetArrayElementAtIndex(index).stringValue = values[index].Trim();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && existing is not T)
                throw new ArgumentException(
                    $"Cannot import {typeof(T).Name} to '{path}' because it contains {existing.GetType().Name}.");
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) { Undo.RegisterCompleteObjectUndo(asset, "Import Narrative Content JSON"); return asset; }
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string segment in path.Replace('\\', '/').Split('/').Skip(1))
            { if (string.IsNullOrWhiteSpace(segment)) continue; string next = $"{current}/{segment}"; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment); current = next; }
        }

        private static string Safe(string value)
        { foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_'); return value.Trim(); }
    }
}
