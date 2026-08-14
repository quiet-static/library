using System;
using System.IO;
using NUnit.Framework;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor;
using QuietStatic.Toolkit.Editor.Dialogue;
using QuietStatic.Toolkit.Editor.Flags;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Interactions;
using QuietStatic.Toolkit.Objectives;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class NarrativeJsonMigrationTests
    {
        [Serializable]
        private sealed class ObjectiveProbe
        {
            public int schemaVersion;
            public string contentType;
            public string catalogId;
            public string unityDatabasePath;
            public ObjectiveItemProbe[] items;
        }

        [Serializable]
        private sealed class FlagProbe
        {
            public string contentType;
            public string unityDatabasePath;
        }

        [Serializable]
        private sealed class ObjectiveItemProbe
        {
            public string id;
            public string title;
            public string unityAssetPath;
            public RequirementProbe activationRequirement;
            public RequirementProbe completionRequirement;
        }

        [Serializable]
        private sealed class RequirementProbe
        {
            public string mode;
            public string[] flags;
        }

        [Serializable]
        private sealed class ReadableProbe
        {
            public string contentType;
            public string catalogId;
            public ReadableItemProbe[] items;
        }

        [Serializable]
        private sealed class ReadableItemProbe
        {
            public string id;
            public string body;
            public string unityAssetPath;
        }

        [Serializable]
        private sealed class DialogueProbe
        {
            public string treeId;
            public string unityAssetPath;
            public string flagCatalog;
            public string startNode;
            public DialogueNodeProbe[] nodes;
        }

        [Serializable]
        private sealed class DialogueNodeProbe
        {
            public string id;
            public string next;
            public DialogueChoiceProbe[] choices;
        }

        [Serializable]
        private sealed class DialogueChoiceProbe
        {
            public string next;
            public RequirementProbe condition;
        }

        private string root;
        private string outputFolder;

        [SetUp]
        public void SetUp()
        {
            root = "Assets/__QuietStaticNarrativeMigrationTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(root));
            outputFolder = Path.Combine(
                Path.GetTempPath(),
                "QuietStaticNarrativeSnapshotTests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(root))
                AssetDatabase.DeleteAsset(root);
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
        }

        [Test]
        public void ProjectSnapshot_PreservesEveryExistingJsonUnlessOverwriteIsExplicit()
        {
            FlagDatabase flags = CreateFlagDatabase();
            ObjectiveDefinition objective = CreateObjective();
            ObjectiveDatabase objectives = CreateObjectiveDatabase(objective);
            ReadableContentDefinition readable = CreateReadable();
            DialogueTree dialogue = CreateDialogue();
            Directory.CreateDirectory(outputFolder);
            string objectiveJson = Path.Combine(outputFolder, "objectives.json");
            File.WriteAllText(objectiveJson, "app-side edit");

            NarrativeAuthoringJsonExporter.SnapshotResult first =
                NarrativeAuthoringJsonExporter.ExportProjectSnapshot(
                    outputFolder,
                    flags,
                    objectives,
                    new[] { readable },
                    new[] { dialogue });

            Assert.That(File.ReadAllText(objectiveJson), Is.EqualTo("app-side edit"));
            Assert.That(first.PreservedPaths, Does.Contain(objectiveJson));
            Assert.That(File.Exists(Path.Combine(outputFolder, "flags.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(outputFolder, "readables.json")), Is.True);

            NarrativeAuthoringJsonExporter.ExportProjectSnapshot(
                outputFolder,
                flags,
                objectives,
                new[] { readable },
                new[] { dialogue },
                overwriteExisting: true);

            StringAssert.Contains("\"contentType\": \"objectives\"", File.ReadAllText(objectiveJson));
        }

        [Test]
        public void FlagExport_IncludesOriginalDatabasePathAndRoundTripsOriginalGuid()
        {
            FlagDatabase database = CreateFlagDatabase();
            string databasePath = AssetDatabase.GetAssetPath(database);
            string guid = AssetDatabase.AssetPathToGUID(databasePath);

            string json = FlagCatalogJsonExporter.BuildJson(database);

            FlagProbe probe = JsonUtility.FromJson<FlagProbe>(json);
            Assert.That(probe.unityDatabasePath, Is.EqualTo(databasePath));
            UnityEngine.Object imported = NarrativeContentJsonImporter.Import(
                WriteJson("flags.json", json),
                root + "/Generated");
            Assert.That(imported, Is.SameAs(database));
            Assert.That(AssetDatabase.AssetPathToGUID(databasePath), Is.EqualTo(guid));
        }

        [Test]
        public void ObjectiveExport_IncludesBothRequirementsAndRoundTripsOriginalAssets()
        {
            ObjectiveDefinition objective = CreateObjective();
            ObjectiveDatabase database = CreateObjectiveDatabase(objective);
            string databasePath = AssetDatabase.GetAssetPath(database);
            string objectivePath = AssetDatabase.GetAssetPath(objective);
            string databaseGuid = AssetDatabase.AssetPathToGUID(databasePath);
            string objectiveGuid = AssetDatabase.AssetPathToGUID(objectivePath);

            string json = NarrativeContentJsonExporter.BuildObjectivesJson(database);

            Assert.DoesNotThrow(() => NarrativeContentJsonImporter.ValidateJson(json));
            ObjectiveProbe probe = JsonUtility.FromJson<ObjectiveProbe>(json);
            Assert.That(probe.schemaVersion, Is.EqualTo(1));
            Assert.That(probe.contentType, Is.EqualTo("objectives"));
            Assert.That(probe.unityDatabasePath, Is.EqualTo(databasePath));
            Assert.That(probe.items[0].unityAssetPath, Is.EqualTo(objectivePath));
            Assert.That(probe.items[0].activationRequirement.mode, Is.EqualTo("All"));
            Assert.That(probe.items[0].activationRequirement.flags,
                Is.EqualTo(new[] { "MetSam" }));
            Assert.That(probe.items[0].completionRequirement.mode, Is.EqualTo("Any"));

            SetString(objective, "title", "Changed after export");
            TextAsset source = WriteJson("objectives.json", json);
            UnityEngine.Object imported = NarrativeContentJsonImporter.Import(source, root + "/Generated");

            Assert.That(imported, Is.SameAs(database));
            Assert.That(objective.Title, Is.EqualTo("Find the letter"));
            Assert.That(AssetDatabase.AssetPathToGUID(databasePath), Is.EqualTo(databaseGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(objectivePath), Is.EqualTo(objectiveGuid));
            Assert.That(AssetDatabase.IsValidFolder(root + "/Generated"), Is.False);
        }

        [Test]
        public void ReadableExport_UsesAssetFilenameAsIdAndRoundTripsOriginalAsset()
        {
            ReadableContentDefinition readable = CreateReadable();
            string readablePath = AssetDatabase.GetAssetPath(readable);
            string guid = AssetDatabase.AssetPathToGUID(readablePath);

            string json = NarrativeContentJsonExporter.BuildReadablesJson(
                new[] { readable },
                "readables");

            Assert.DoesNotThrow(() => NarrativeContentJsonImporter.ValidateJson(json));
            ReadableProbe probe = JsonUtility.FromJson<ReadableProbe>(json);
            Assert.That(probe.items[0].id, Is.EqualTo("LoveLetter"));
            Assert.That(probe.items[0].unityAssetPath, Is.EqualTo(readablePath));
            SetString(readable, "body", "Changed after export");
            UnityEngine.Object imported = NarrativeContentJsonImporter.Import(
                WriteJson("readables.json", json),
                root + "/Generated");

            Assert.That(imported, Is.SameAs(readable));
            Assert.That(readable.Body, Is.EqualTo("Meet me after dark."));
            Assert.That(AssetDatabase.AssetPathToGUID(readablePath), Is.EqualTo(guid));
        }

        [Test]
        public void DialogueExport_PreservesLinearFallbackChoicesConditionsAndOriginalGuid()
        {
            DialogueTree tree = CreateDialogue();
            string treePath = AssetDatabase.GetAssetPath(tree);
            string guid = AssetDatabase.AssetPathToGUID(treePath);

            string json = DialogueJsonExporter.BuildJson(
                tree,
                flagCatalog: "../../flags.json");

            Assert.DoesNotThrow(() => DialogueJsonImporter.ValidateJson(json));
            DialogueProbe probe = JsonUtility.FromJson<DialogueProbe>(json);
            Assert.That(probe.unityAssetPath, Is.EqualTo(treePath));
            Assert.That(probe.flagCatalog, Is.EqualTo("../../flags.json"));
            Assert.That(probe.startNode, Is.EqualTo("node_001"));
            Assert.That(probe.nodes[0].next, Is.EqualTo("ending"));
            StringAssert.Contains("\"next\": null", json);
            Assert.That(probe.nodes[0].choices[0].next, Is.Empty);
            Assert.That(probe.nodes[0].choices[0].condition.mode, Is.EqualTo("All"));
            Assert.That(probe.nodes[1].choices, Is.Empty);

            SetDialogueLine(tree, 0, "Changed after export");
            DialogueTree imported = DialogueJsonImporter.Import(
                WriteJson("dialogue.json", json),
                root + "/Generated");

            Assert.That(imported, Is.SameAs(tree));
            Assert.That(tree.Nodes[0].line, Is.EqualTo("Original line"));
            Assert.That(tree.Nodes[0].nextNodeIndex, Is.EqualTo(1));
            Assert.That(tree.Nodes[0].choices[0].nextNodeIndex, Is.EqualTo(-1));
            Assert.That(AssetDatabase.AssetPathToGUID(treePath), Is.EqualTo(guid));
            Assert.That(AssetDatabase.IsValidFolder(root + "/Generated"), Is.False);
        }

        [Test]
        public void ImportValidation_RejectsMetadataPointingToWrongAssetType()
        {
            ObjectiveDefinition objective = CreateObjective();
            string json = $@"{{
  ""schemaVersion"":1,
  ""treeId"":""wrong_target"",
  ""unityAssetPath"":""{AssetDatabase.GetAssetPath(objective)}"",
  ""startNode"":""start"",
  ""nodes"":[{{
    ""id"":""start"",""speaker"":""Narrator"",""text"":""Text"",
    ""next"":null,""choices"":[]
  }}]
}}";

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                DialogueJsonImporter.ValidateJson(json));

            StringAssert.Contains("not DialogueTree", exception.Message);
        }

        [Test]
        public void ObjectiveValidation_RejectsInvalidActivationRequirement()
        {
            const string json = @"{
  ""schemaVersion"":1,""contentType"":""objectives"",""catalogId"":""objectives"",
  ""items"":[{
    ""id"":""find_letter"",""title"":""Find letter"",""description"":"""",
    ""activationRequirement"":{ ""mode"":""All"", ""flags"":[] },
    ""completionRequirement"":{ ""mode"":""None"", ""flags"":[] }
  }]
}";

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                NarrativeContentJsonImporter.ValidateJson(json));

            StringAssert.Contains("activation requirement needs flags", exception.Message);
        }

        private ObjectiveDefinition CreateObjective()
        {
            var objective = ScriptableObject.CreateInstance<ObjectiveDefinition>();
            var serialized = new SerializedObject(objective);
            serialized.FindProperty("id").stringValue = "find_letter";
            serialized.FindProperty("title").stringValue = "Find the letter";
            serialized.FindProperty("description").stringValue = "Look in the bedroom.";
            SetRequirement(
                serialized.FindProperty("activationRequirement"),
                FlagRequirementMode.All,
                "MetSam");
            SetRequirement(
                serialized.FindProperty("completionRequirement"),
                FlagRequirementMode.Any,
                "ReadLetter",
                "FoundPhoto");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(objective, root + "/FindLetter.asset");
            return objective;
        }

        private FlagDatabase CreateFlagDatabase()
        {
            var database = ScriptableObject.CreateInstance<FlagDatabase>();
            var serialized = new SerializedObject(database);
            SerializedProperty flags = serialized.FindProperty("flags");
            flags.arraySize = 1;
            flags.GetArrayElementAtIndex(0).FindPropertyRelative("id").stringValue = "StoryStarted";
            flags.GetArrayElementAtIndex(0).FindPropertyRelative("description").stringValue = "Story began.";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(database, root + "/FlagsDB.asset");
            AssetDatabase.SaveAssets();
            return database;
        }

        private ObjectiveDatabase CreateObjectiveDatabase(ObjectiveDefinition objective)
        {
            var database = ScriptableObject.CreateInstance<ObjectiveDatabase>();
            var serialized = new SerializedObject(database);
            SerializedProperty objectives = serialized.FindProperty("objectives");
            objectives.arraySize = 1;
            objectives.GetArrayElementAtIndex(0).objectReferenceValue = objective;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(database, root + "/ObjectivesDB.asset");
            AssetDatabase.SaveAssets();
            return database;
        }

        private ReadableContentDefinition CreateReadable()
        {
            var readable = ScriptableObject.CreateInstance<ReadableContentDefinition>();
            var serialized = new SerializedObject(readable);
            serialized.FindProperty("title").stringValue = "Love Letter";
            serialized.FindProperty("body").stringValue = "Meet me after dark.";
            serialized.FindProperty("closeLabel").stringValue = "Fold";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(readable, root + "/LoveLetter.asset");
            AssetDatabase.SaveAssets();
            return readable;
        }

        private DialogueTree CreateDialogue()
        {
            var tree = ScriptableObject.CreateInstance<DialogueTree>();
            var serialized = new SerializedObject(tree);
            SerializedProperty nodes = serialized.FindProperty("nodes");
            nodes.arraySize = 2;
            SerializedProperty first = nodes.GetArrayElementAtIndex(0);
            first.FindPropertyRelative("id").stringValue = string.Empty;
            first.FindPropertyRelative("speaker").stringValue = "Narrator";
            first.FindPropertyRelative("line").stringValue = "Original line";
            first.FindPropertyRelative("nextNodeIndex").intValue = 1;
            first.FindPropertyRelative("flagsToSetOnEnter").arraySize = 0;
            SerializedProperty choices = first.FindPropertyRelative("choices");
            choices.arraySize = 1;
            SerializedProperty choice = choices.GetArrayElementAtIndex(0);
            choice.FindPropertyRelative("text").stringValue = "Wait";
            choice.FindPropertyRelative("nextNodeIndex").intValue = -1;
            choice.FindPropertyRelative("flagsToSet").arraySize = 0;
            SetRequirement(
                choice.FindPropertyRelative("availabilityRequirement"),
                FlagRequirementMode.All,
                "CanWait");

            SerializedProperty second = nodes.GetArrayElementAtIndex(1);
            second.FindPropertyRelative("id").stringValue = "ending";
            second.FindPropertyRelative("speaker").stringValue = "Narrator";
            second.FindPropertyRelative("line").stringValue = "End";
            second.FindPropertyRelative("nextNodeIndex").intValue = -1;
            second.FindPropertyRelative("flagsToSetOnEnter").arraySize = 0;
            second.FindPropertyRelative("choices").arraySize = 0;
            serialized.FindProperty("startNodeIndex").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(tree, root + "/LegacyDialogue.asset");
            AssetDatabase.SaveAssets();
            return tree;
        }

        private TextAsset WriteJson(string filename, string json)
        {
            string path = root + "/" + filename;
            File.WriteAllText(path, json);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        }

        private static void SetRequirement(
            SerializedProperty requirement,
            FlagRequirementMode mode,
            params string[] flags)
        {
            requirement.FindPropertyRelative("mode").enumValueIndex = (int)mode;
            SerializedProperty values = requirement.FindPropertyRelative("flags");
            values.arraySize = flags.Length;
            for (int index = 0; index < flags.Length; index++)
                values.GetArrayElementAtIndex(index).stringValue = flags[index];
        }

        private static void SetString(
            UnityEngine.Object target,
            string property,
            string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetDialogueLine(DialogueTree tree, int index, string value)
        {
            var serialized = new SerializedObject(tree);
            serialized.FindProperty("nodes")
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative("line")
                .stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
