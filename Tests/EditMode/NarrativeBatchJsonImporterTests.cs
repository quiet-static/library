using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Interactions;
using QuietStatic.Toolkit.Objectives;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class NarrativeBatchJsonImporterTests
    {
        private string exportFolder;
        private string assetRoot;
        private string narrativeOutputFolder;
        private string dialogueOutputFolder;

        [SetUp]
        public void SetUp()
        {
            exportFolder = Path.Combine(
                Path.GetTempPath(),
                "QuietStaticNarrativeBatchImporterTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(exportFolder);
            assetRoot = $"Assets/__QuietStaticNarrativeBatchImporterTests_{Guid.NewGuid():N}";
            narrativeOutputFolder = assetRoot + "/Narrative";
            dialogueOutputFolder = assetRoot + "/Dialogue";
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(assetRoot))
                AssetDatabase.DeleteAsset(assetRoot);
            if (Directory.Exists(exportFolder))
                Directory.Delete(exportFolder, true);
        }

        [Test]
        public void Preflight_OrdersEverySupportedKindWithoutMutatingAssets()
        {
            Write("dialogue/intro.json", DialogueJson("intro"));
            Write("readables/notes.json", ContentJson("readables", "notes"));
            Write("objectives/main.json", ContentJson("objectives", "main"));
            Write("flags/story.json", ContentJson("flags", "story"));
            WriteManifest(
                ("dialogue/intro.json", "dialogue"),
                ("readables/notes.json", "readables"),
                ("objectives/main.json", "objectives"),
                ("flags/story.json", "flags"));

            NarrativeBatchJsonImporter.Plan plan =
                NarrativeBatchJsonImporter.Preflight(exportFolder);

            CollectionAssert.AreEqual(
                new[]
                {
                    NarrativeBatchJsonImporter.DocumentKind.Flags,
                    NarrativeBatchJsonImporter.DocumentKind.Objectives,
                    NarrativeBatchJsonImporter.DocumentKind.Readables,
                    NarrativeBatchJsonImporter.DocumentKind.Dialogue,
                },
                plan.Documents.Select(document => document.Kind));
            CollectionAssert.AreEqual(
                new[] { "story", "main", "notes", "intro" },
                plan.Documents.Select(document => document.Identity));
            Assert.That(AssetDatabase.IsValidFolder(assetRoot), Is.False);
        }

        [Test]
        public void Preflight_DescribesEveryAffectedAssetWithoutMutatingOutputs()
        {
            Write("dialogue/intro.json", DialogueJson("intro"));
            Write(
                "readables/notes.json",
                ContentJson(
                    "readables",
                    "notes",
                    @"{""id"":""note"",""title"":""Note"",""body"":""Body""}"));
            Write(
                "objectives/main.json",
                ContentJson(
                    "objectives",
                    "main",
                    @"{""id"":""first"",""title"":""First""},{""id"":""second"",""title"":""Second""}"));
            Write("flags/story.json", ContentJson("flags", "story"));
            WriteManifest(
                ("dialogue/intro.json", "dialogue"),
                ("readables/notes.json", "readables"),
                ("objectives/main.json", "objectives"),
                ("flags/story.json", "flags"));

            NarrativeBatchJsonImporter.Plan plan = NarrativeBatchJsonImporter.Preflight(
                exportFolder,
                narrativeOutputFolder,
                dialogueOutputFolder);

            CollectionAssert.AreEqual(
                new[]
                {
                    narrativeOutputFolder + "/story/story.asset",
                    narrativeOutputFolder + "/main/main.asset",
                    narrativeOutputFolder + "/main/first.asset",
                    narrativeOutputFolder + "/main/second.asset",
                    narrativeOutputFolder + "/notes/note.asset",
                    dialogueOutputFolder + "/intro.asset",
                },
                plan.AssetChanges.Select(change => change.AssetPath));
            CollectionAssert.AreEqual(
                new[]
                {
                    typeof(FlagDatabase),
                    typeof(ObjectiveDatabase),
                    typeof(ObjectiveDefinition),
                    typeof(ObjectiveDefinition),
                    typeof(ReadableContentDefinition),
                    typeof(DialogueTree),
                },
                plan.AssetChanges.Select(change => change.AssetType));
            Assert.That(
                plan.AssetChanges.All(
                    change => change.Kind == NarrativeBatchJsonImporter.AssetChangeKind.Create),
                Is.True);
            Assert.That(
                plan.AssetChanges.All(change => change.SourceDocument != null),
                Is.True);
            Assert.That(AssetDatabase.IsValidFolder(assetRoot), Is.False);
        }

        [Test]
        public void ImportFolder_ImportsFlagsBeforeDialogueAndPreservesAssetGuids()
        {
            Write("content/flags.json", ContentJson("flags", "authorer_flags",
                @"{""id"":""door.open"",""description"":""The door was opened.""}"));
            Write("dialogue/intro.json", DialogueJson("intro", "door.open"));
            WriteManifest(
                ("dialogue/intro.json", "dialogue"),
                ("content/flags.json", "flags"));

            NarrativeBatchJsonImporter.Result first = NarrativeBatchJsonImporter.Import(
                exportFolder,
                narrativeOutputFolder,
                dialogueOutputFolder);

            Assert.That(first.ImportedAssets[0], Is.TypeOf<FlagDatabase>());
            Assert.That(first.ImportedAssets[1], Is.TypeOf<DialogueTree>());
            FlagDatabase flags = (FlagDatabase)first.ImportedAssets[0];
            DialogueTree dialogue = (DialogueTree)first.ImportedAssets[1];
            Assert.That(flags.Flags.Select(flag => flag.id), Is.EqualTo(new[] { "door.open" }));
            Assert.That(dialogue.Nodes[0].flagsToSetOnEnter, Is.EqualTo(new[] { "door.open" }));

            string flagPath = narrativeOutputFolder + "/authorer_flags/authorer_flags.asset";
            string dialoguePath = dialogueOutputFolder + "/intro.asset";
            string flagGuid = AssetDatabase.AssetPathToGUID(flagPath);
            string dialogueGuid = AssetDatabase.AssetPathToGUID(dialoguePath);
            Assert.That(flagGuid, Is.Not.Empty);
            Assert.That(dialogueGuid, Is.Not.Empty);

            NarrativeBatchJsonImporter.Plan updatePlan =
                NarrativeBatchJsonImporter.Preflight(
                    exportFolder,
                    narrativeOutputFolder,
                    dialogueOutputFolder);
            Assert.That(
                updatePlan.AssetChanges.All(
                    change => change.Kind == NarrativeBatchJsonImporter.AssetChangeKind.Update),
                Is.True);

            NarrativeBatchJsonImporter.Result second =
                NarrativeBatchJsonImporter.ImportReviewedPlan(updatePlan);

            Assert.That(second.ImportedAssets[0], Is.SameAs(first.ImportedAssets[0]));
            Assert.That(second.ImportedAssets[1], Is.SameAs(first.ImportedAssets[1]));
            Assert.That(AssetDatabase.AssetPathToGUID(flagPath), Is.EqualTo(flagGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(dialoguePath), Is.EqualTo(dialogueGuid));
        }

        [Test]
        public void Import_ReplaceObjectivesBlockedByExternalReferenceFailsBeforeFlagsChange()
        {
            EnsureAssetRoot();
            ObjectiveDefinition objective = CreateObjective(
                assetRoot + "/LegacyReferenced.asset",
                "referenced",
                "Original title");
            ObjectiveDatabase database = CreateObjectiveDatabase(
                assetRoot + "/ObjectivesDB.asset",
                objective);
            string objectivePath = AssetDatabase.GetAssetPath(objective);
            string databasePath = AssetDatabase.GetAssetPath(database);
            string prefabPath = CreateObjectiveHandlerPrefab(
                assetRoot + "/ExternalObjectiveReference.prefab",
                objective);
            string objectiveGuid = AssetDatabase.AssetPathToGUID(objectivePath);
            string databaseGuid = AssetDatabase.AssetPathToGUID(databasePath);
            string flagPath = narrativeOutputFolder + "/story/story.asset";
            Write(
                "content/flags.json",
                ContentJson(
                    "flags",
                    "story",
                    @"{""id"":""ready"",""description"":""Ready""}"));
            Write(
                "content/objectives.json",
                ObjectiveReplacementJson(databasePath));
            WriteManifest(
                ("content/flags.json", "flags"),
                ("content/objectives.json", "objectives"));
            CollectionAssert.Contains(
                AssetDatabase.GetDependencies(prefabPath, false),
                objectivePath,
                "The fixture must contain a direct serialized objective reference.");

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                NarrativeBatchJsonImporter.Import(
                    exportFolder,
                    narrativeOutputFolder,
                    dialogueOutputFolder));

            StringAssert.Contains(objectivePath, exception.Message);
            StringAssert.Contains(prefabPath, exception.Message);
            Assert.That(AssetDatabase.IsValidFolder(narrativeOutputFolder), Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<FlagDatabase>(flagPath), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<ObjectiveDatabase>(databasePath),
                Is.SameAs(database));
            Assert.That(AssetDatabase.AssetPathToGUID(databasePath), Is.EqualTo(databaseGuid));
            Assert.That(database.Objectives.Count, Is.EqualTo(1));
            Assert.That(database.Objectives[0], Is.SameAs(objective));
            Assert.That(objective.Title, Is.EqualTo("Original title"));
            Assert.That(AssetDatabase.LoadAssetAtPath<ObjectiveDefinition>(objectivePath),
                Is.SameAs(objective));
            Assert.That(AssetDatabase.AssetPathToGUID(objectivePath), Is.EqualTo(objectiveGuid));
        }

        [Test]
        public void Preflight_DescribesObjectiveRegenerationAndDeletionWithoutApplyingThem()
        {
            string regeneratedPath =
                narrativeOutputFolder + "/main/Definitions/referenced.asset";
            ObjectiveDefinition regenerated = CreateObjective(
                regeneratedPath,
                "referenced",
                "Original title");
            ObjectiveDefinition deleted = CreateObjective(
                assetRoot + "/LegacyRemoved.asset",
                "removed",
                "Removed title");
            ObjectiveDatabase database = CreateObjectiveDatabase(
                assetRoot + "/ObjectivesDB.asset",
                regenerated,
                deleted);
            string databasePath = AssetDatabase.GetAssetPath(database);
            string regeneratedGuid = AssetDatabase.AssetPathToGUID(regeneratedPath);
            string deletedPath = AssetDatabase.GetAssetPath(deleted);
            string deletedGuid = AssetDatabase.AssetPathToGUID(deletedPath);
            Write("content/objectives.json", ObjectiveReplacementJson(databasePath));
            WriteManifest(("content/objectives.json", "objectives"));

            NarrativeBatchJsonImporter.Plan plan = NarrativeBatchJsonImporter.Preflight(
                exportFolder,
                narrativeOutputFolder,
                dialogueOutputFolder);

            Assert.That(plan.AssetChanges.Count, Is.EqualTo(3));
            Assert.That(
                plan.AssetChanges.Single(change => change.AssetPath == databasePath).Kind,
                Is.EqualTo(NarrativeBatchJsonImporter.AssetChangeKind.Update));
            Assert.That(
                plan.AssetChanges.Single(change => change.AssetPath == regeneratedPath).Kind,
                Is.EqualTo(NarrativeBatchJsonImporter.AssetChangeKind.Regenerate));
            Assert.That(
                plan.AssetChanges.Single(change => change.AssetPath == deletedPath).Kind,
                Is.EqualTo(NarrativeBatchJsonImporter.AssetChangeKind.Delete));
            Assert.That(AssetDatabase.LoadAssetAtPath<ObjectiveDefinition>(regeneratedPath),
                Is.SameAs(regenerated));
            Assert.That(AssetDatabase.AssetPathToGUID(regeneratedPath), Is.EqualTo(regeneratedGuid));
            Assert.That(AssetDatabase.LoadAssetAtPath<ObjectiveDefinition>(deletedPath),
                Is.SameAs(deleted));
            Assert.That(AssetDatabase.AssetPathToGUID(deletedPath), Is.EqualTo(deletedGuid));
        }

        [Test]
        public void ImportReviewedPlan_RejectsSourceChangesBeforeMutatingAssets()
        {
            Write("content/flags.json", ContentJson("flags", "story"));
            WriteManifest(("content/flags.json", "flags"));
            NarrativeBatchJsonImporter.Plan reviewed = NarrativeBatchJsonImporter.Preflight(
                exportFolder,
                narrativeOutputFolder,
                dialogueOutputFolder);
            Write(
                "content/flags.json",
                ContentJson(
                    "flags",
                    "story",
                    @"{""id"":""changed"",""description"":""Changed""}"));

            Assert.Throws<NarrativeBatchJsonImporter.PreviewOutOfDateException>(() =>
                NarrativeBatchJsonImporter.ImportReviewedPlan(reviewed));

            Assert.That(AssetDatabase.IsValidFolder(assetRoot), Is.False);
        }

        [Test]
        public void ImportReviewedPlan_RejectsTargetStateChangesBeforeWriting()
        {
            Write("content/flags.json", ContentJson("flags", "story"));
            WriteManifest(("content/flags.json", "flags"));
            NarrativeBatchJsonImporter.Plan reviewed = NarrativeBatchJsonImporter.Preflight(
                exportFolder,
                narrativeOutputFolder,
                dialogueOutputFolder);
            string targetPath = narrativeOutputFolder + "/story/story.asset";
            EnsureAssetFolderForPath(targetPath);
            var existing = ScriptableObject.CreateInstance<FlagDatabase>();
            AssetDatabase.CreateAsset(existing, targetPath);
            AssetDatabase.SaveAssets();

            Assert.Throws<NarrativeBatchJsonImporter.PreviewOutOfDateException>(() =>
                NarrativeBatchJsonImporter.ImportReviewedPlan(reviewed));

            Assert.That(AssetDatabase.LoadAssetAtPath<FlagDatabase>(targetPath),
                Is.SameAs(existing));
            Assert.That(existing.Flags == null || !existing.Flags.Any(), Is.True);
        }

        [Test]
        public void ImportReviewedPlan_RejectsSameTypeTargetReplacementBeforeWriting()
        {
            Write("content/flags.json", ContentJson("flags", "story"));
            WriteManifest(("content/flags.json", "flags"));
            string targetPath = narrativeOutputFolder + "/story/story.asset";
            EnsureAssetFolderForPath(targetPath);
            var original = ScriptableObject.CreateInstance<FlagDatabase>();
            AssetDatabase.CreateAsset(original, targetPath);
            AssetDatabase.SaveAssets();
            NarrativeBatchJsonImporter.Plan reviewed = NarrativeBatchJsonImporter.Preflight(
                exportFolder,
                narrativeOutputFolder,
                dialogueOutputFolder);
            string originalGuid = AssetDatabase.AssetPathToGUID(targetPath);

            string replacementPath = narrativeOutputFolder + "/story/replacement.asset";
            var replacement = ScriptableObject.CreateInstance<FlagDatabase>();
            AssetDatabase.CreateAsset(replacement, replacementPath);
            string replacementGuid = AssetDatabase.AssetPathToGUID(replacementPath);
            Assert.That(replacementGuid, Is.Not.EqualTo(originalGuid));
            Assert.That(AssetDatabase.DeleteAsset(targetPath), Is.True);
            Assert.That(AssetDatabase.MoveAsset(replacementPath, targetPath), Is.Empty);
            AssetDatabase.SaveAssets();
            Assert.That(AssetDatabase.AssetPathToGUID(targetPath), Is.EqualTo(replacementGuid));

            Assert.Throws<NarrativeBatchJsonImporter.PreviewOutOfDateException>(() =>
                NarrativeBatchJsonImporter.ImportReviewedPlan(reviewed));

            Assert.That(AssetDatabase.LoadAssetAtPath<FlagDatabase>(targetPath),
                Is.SameAs(replacement));
            Assert.That(replacement.Flags == null || !replacement.Flags.Any(), Is.True);
        }

        [Test]
        public void ImportReviewedPlan_RejectsTargetContentChangesBeforeWriting()
        {
            Write("content/flags.json", ContentJson("flags", "story"));
            WriteManifest(("content/flags.json", "flags"));
            string targetPath = narrativeOutputFolder + "/story/story.asset";
            EnsureAssetFolderForPath(targetPath);
            var existing = ScriptableObject.CreateInstance<FlagDatabase>();
            AssetDatabase.CreateAsset(existing, targetPath);
            AssetDatabase.SaveAssets();
            NarrativeBatchJsonImporter.Plan reviewed = NarrativeBatchJsonImporter.Preflight(
                exportFolder,
                narrativeOutputFolder,
                dialogueOutputFolder);

            var serialized = new SerializedObject(existing);
            SerializedProperty flags = serialized.FindProperty("flags");
            flags.arraySize = 1;
            SerializedProperty flag = flags.GetArrayElementAtIndex(0);
            flag.FindPropertyRelative("id").stringValue = "local.change";
            flag.FindPropertyRelative("description").stringValue = "Local edit";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();

            Assert.Throws<NarrativeBatchJsonImporter.PreviewOutOfDateException>(() =>
                NarrativeBatchJsonImporter.ImportReviewedPlan(reviewed));

            FlagDatabase unchanged = AssetDatabase.LoadAssetAtPath<FlagDatabase>(targetPath);
            Assert.That(unchanged.Flags.Select(value => value.id),
                Is.EqualTo(new[] { "local.change" }));
        }

        [Test]
        public void ImportReviewedPlan_RejectsUnsavedTargetContentChangesBeforeWriting()
        {
            Write("content/flags.json", ContentJson("flags", "story"));
            WriteManifest(("content/flags.json", "flags"));
            string targetPath = narrativeOutputFolder + "/story/story.asset";
            EnsureAssetFolderForPath(targetPath);
            var existing = ScriptableObject.CreateInstance<FlagDatabase>();
            AssetDatabase.CreateAsset(existing, targetPath);
            AssetDatabase.SaveAssets();
            NarrativeBatchJsonImporter.Plan reviewed = NarrativeBatchJsonImporter.Preflight(
                exportFolder,
                narrativeOutputFolder,
                dialogueOutputFolder);

            var serialized = new SerializedObject(existing);
            SerializedProperty flags = serialized.FindProperty("flags");
            flags.arraySize = 1;
            SerializedProperty flag = flags.GetArrayElementAtIndex(0);
            flag.FindPropertyRelative("id").stringValue = "unsaved.change";
            flag.FindPropertyRelative("description").stringValue = "Unsaved local edit";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(existing);

            Assert.Throws<NarrativeBatchJsonImporter.PreviewOutOfDateException>(() =>
                NarrativeBatchJsonImporter.ImportReviewedPlan(reviewed));

            Assert.That(existing.Flags.Select(value => value.id),
                Is.EqualTo(new[] { "unsaved.change" }));
        }

        [Test]
        public void Import_RejectsKindMismatchBeforeCreatingOutputFolders()
        {
            Write("content/flags.json", ContentJson("flags", "story"));
            Write("content/wrong.json", ContentJson("objectives", "wrong"));
            WriteManifest(
                ("content/flags.json", "flags"),
                ("content/wrong.json", "dialogue"));

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                NarrativeBatchJsonImporter.Import(
                    exportFolder,
                    narrativeOutputFolder,
                    dialogueOutputFolder));

            StringAssert.Contains("kind dialogue", exception.Message);
            Assert.That(AssetDatabase.IsValidFolder(assetRoot), Is.False);
        }

        [Test]
        public void Import_RejectsInvalidNestedJsonBeforeCreatingOutputFolders()
        {
            Write("content/flags.json", ContentJson("flags", "story"));
            Write(
                "dialogue/invalid.json",
                @"{
  ""schemaVersion"":1,
  ""treeId"":""invalid"",
  ""startNode"":""start"",
  ""nodes"":[{
    ""id"":""start"",
    ""speaker"":""Narrator"",
    ""text"":""Hello"",
    ""next"":""missing"",
    ""choices"":[]
  }]
}");
            WriteManifest(
                ("content/flags.json", "flags"),
                ("dialogue/invalid.json", "dialogue"));

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                NarrativeBatchJsonImporter.Import(
                    exportFolder,
                    narrativeOutputFolder,
                    dialogueOutputFolder));

            StringAssert.Contains("nonexistent node", exception.Message);
            Assert.That(AssetDatabase.IsValidFolder(assetRoot), Is.False);
        }

        [Test]
        public void Preflight_RejectsIdentitiesThatMapToTheSameGeneratedAsset()
        {
            Write("dialogue/first.json", DialogueJson("same:name"));
            Write("dialogue/second.json", DialogueJson("SAME?NAME"));
            WriteManifest(
                ("dialogue/first.json", "dialogue"),
                ("dialogue/second.json", "dialogue"));

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                NarrativeBatchJsonImporter.Preflight(exportFolder));

            StringAssert.Contains("same generated asset name", exception.Message);
            Assert.That(AssetDatabase.IsValidFolder(assetRoot), Is.False);
        }

        [Test]
        public void Preflight_RejectsWindowsReservedGeneratedAssetNames()
        {
            Write("dialogue/conversation.json", DialogueJson("CON"));
            WriteManifest(("dialogue/conversation.json", "dialogue"));

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                NarrativeBatchJsonImporter.Preflight(exportFolder));

            StringAssert.Contains("Windows-reserved", exception.Message);
            Assert.That(AssetDatabase.IsValidFolder(assetRoot), Is.False);
        }

        [Test]
        public void Preflight_RejectsCaseInsensitiveUnityTargetCollisions()
        {
            string sharedPath = assetRoot + "/Shared.asset";
            string alternateCasePath = "Assets/" +
                                       sharedPath.Substring("Assets/".Length).ToUpperInvariant();
            Write(
                "content/flags.json",
                $@"{{""schemaVersion"":1,""contentType"":""flags"",""catalogId"":""story"",""unityDatabasePath"":""{sharedPath}"",""items"":[{{""id"":""ready"",""description"":""Ready""}}]}}");
            Write(
                "dialogue/intro.json",
                DialogueJson("intro").Replace(
                    "\"startNode\"",
                    $"\"unityAssetPath\":\"{alternateCasePath}\",\"startNode\""));
            WriteManifest(
                ("content/flags.json", "flags"),
                ("dialogue/intro.json", "dialogue"));

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                NarrativeBatchJsonImporter.Preflight(exportFolder));

            StringAssert.Contains("same Unity asset", exception.Message);
            Assert.That(AssetDatabase.IsValidFolder(assetRoot), Is.False);
        }

        [Test]
        public void Preflight_RejectsExplicitTargetCollidingWithAnotherDefaultTarget()
        {
            string defaultTarget = NarrativeBatchJsonImporter.DefaultDialogueOutputFolder +
                                   "/first.asset";
            Write("dialogue/first.json", DialogueJson("first"));
            Write(
                "dialogue/second.json",
                DialogueJson("second").Replace(
                    "\"startNode\"",
                    $"\"unityAssetPath\":\"{defaultTarget}\",\"startNode\""));
            WriteManifest(
                ("dialogue/first.json", "dialogue"),
                ("dialogue/second.json", "dialogue"));

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                NarrativeBatchJsonImporter.Preflight(exportFolder));

            StringAssert.Contains("same Unity asset", exception.Message);
        }

        [Test]
        public void Preflight_RejectsObjectiveItemCollidingWithItsDefaultDatabaseTarget()
        {
            Write(
                "content/objectives.json",
                ContentJson(
                    "objectives",
                    "same",
                    @"{""id"":""same"",""title"":""Same"",""completionRequirement"":{""mode"":""None"",""flags"":[]}}"));
            WriteManifest(("content/objectives.json", "objectives"));

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                NarrativeBatchJsonImporter.Preflight(exportFolder));

            StringAssert.Contains("same Unity asset", exception.Message);
        }

        private void WriteManifest(params (string path, string kind)[] documents)
        {
            string entries = string.Join(
                ",",
                documents.Select(document =>
                    $@"{{""path"":""{document.path}"",""kind"":""{document.kind}""}}"));
            Write(
                NarrativeBatchJsonImporter.ManifestFileName,
                $@"{{""schemaVersion"":1,""documents"":[{entries}]}}");
        }

        private void Write(string relativePath, string contents)
        {
            string path = Path.Combine(exportFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);
        }

        private void EnsureAssetRoot()
        {
            if (!AssetDatabase.IsValidFolder(assetRoot))
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(assetRoot));
        }

        private static ObjectiveDefinition CreateObjective(
            string path,
            string id,
            string title)
        {
            EnsureAssetFolderForPath(path);
            var objective = ScriptableObject.CreateInstance<ObjectiveDefinition>();
            var serialized = new SerializedObject(objective);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("title").stringValue = title;
            serialized.FindProperty("description").stringValue = string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(objective, path);
            return objective;
        }

        private static ObjectiveDatabase CreateObjectiveDatabase(
            string path,
            params ObjectiveDefinition[] objectivesToAdd)
        {
            EnsureAssetFolderForPath(path);
            var database = ScriptableObject.CreateInstance<ObjectiveDatabase>();
            var serialized = new SerializedObject(database);
            SerializedProperty objectives = serialized.FindProperty("objectives");
            objectives.arraySize = objectivesToAdd.Length;
            for (int index = 0; index < objectivesToAdd.Length; index++)
            {
                objectives.GetArrayElementAtIndex(index).objectReferenceValue =
                    objectivesToAdd[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(database, path);
            AssetDatabase.SaveAssets();
            return database;
        }

        private static void EnsureAssetFolderForPath(string assetPath)
        {
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(folder) || folder == "Assets")
                return;

            string current = "Assets";
            foreach (string segment in folder.Split('/').Skip(1))
            {
                string child = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(child))
                    AssetDatabase.CreateFolder(current, segment);
                current = child;
            }
        }

        private static string CreateObjectiveHandlerPrefab(
            string path,
            ObjectiveDefinition objective)
        {
            var gameObject = new GameObject("External Objective Reference");
            try
            {
                var handler = gameObject.AddComponent<global::QuietStatic.ObjectiveHandler>();
                var serialized = new SerializedObject(handler);
                serialized.FindProperty("objective").objectReferenceValue = objective;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(gameObject, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
            AssetDatabase.SaveAssets();
            return path;
        }

        private static string ObjectiveReplacementJson(string databasePath) => $@"{{
  ""schemaVersion"":1,
  ""contentType"":""objectives"",
  ""catalogId"":""main"",
  ""unityDatabasePath"":""{databasePath}"",
  ""unityObjectiveImportMode"":""Replace"",
  ""items"":[{{""id"":""referenced"",""title"":""Replacement title""}}]
}}";

        private static string ContentJson(string kind, string catalogId, string item = null)
        {
            item ??= @"{""id"":""entry"",""title"":""Entry"",""body"":""Body""}";
            return $@"{{""schemaVersion"":1,""contentType"":""{kind}"",""catalogId"":""{catalogId}"",""items"":[{item}]}}";
        }

        private static string DialogueJson(string treeId, string flag = null)
        {
            string flags = flag == null
                ? string.Empty
                : $@",""flagsToSetOnEnter"": [""{flag}""]";
            return $@"{{
  ""schemaVersion"":1,
  ""treeId"":""{treeId}"",
  ""startNode"":""start"",
  ""nodes"":[{{
    ""id"":""start"",
    ""speaker"":""Narrator"",
    ""text"":""Hello""{flags},
    ""choices"":[{{""text"":""End"",""next"":null}}]
  }}]
}}";
        }
    }
}
