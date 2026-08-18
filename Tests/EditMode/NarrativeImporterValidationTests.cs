using System;
using System.IO;
using NUnit.Framework;
using QuietStatic.Toolkit.Editor;
using QuietStatic.Toolkit.Editor.Dialogue;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Objectives;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class NarrativeImporterValidationTests
    {
        private string root;
        private string invalidOutputFolder;
        private string invalidShadowFolder;

        [SetUp]
        public void SetUp()
        {
            string suffix = Guid.NewGuid().ToString("N");
            root = "Assets/__QuietStaticNarrativeImporterValidationTests_" + suffix;
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(root));
            invalidOutputFolder = "AssetsSibling_" + suffix + "/Generated";
            invalidShadowFolder = "Assets/AssetsSibling_" + suffix;
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(root))
                AssetDatabase.DeleteAsset(root);
            if (AssetDatabase.IsValidFolder(invalidShadowFolder))
                AssetDatabase.DeleteAsset(invalidShadowFolder);
        }

        [Test]
        public void DialogueImport_RejectsAssetsSiblingOutputWithoutCreatingFolders()
        {
            WithTextAsset(DialogueJson(), source =>
            {
                ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                    DialogueJsonImporter.Import(source, invalidOutputFolder));

                Assert.That(exception.ParamName, Is.EqualTo("outputFolder"));
                StringAssert.Contains("under Assets", exception.Message);
                Assert.That(AssetDatabase.IsValidFolder(invalidShadowFolder), Is.False);
            });
        }

        [Test]
        public void NarrativeImport_RejectsAssetsSiblingOutputWithoutCreatingFolders()
        {
            WithTextAsset(FlagsJson(), source =>
            {
                ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                    NarrativeContentJsonImporter.Import(source, invalidOutputFolder));

                Assert.That(exception.ParamName, Is.EqualTo("outputFolder"));
                StringAssert.Contains("under Assets", exception.Message);
                Assert.That(AssetDatabase.IsValidFolder(invalidShadowFolder), Is.False);
            });
        }

        [TestCase("-1")]
        [TestCase("999")]
        public void DialogueValidation_RejectsUndefinedNumericRequirementMode(string mode)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                DialogueJsonImporter.ValidateJson(DialogueJson(mode)));

            StringAssert.Contains(
                ".mode must be None, All, Any, NotAll, or NotAny",
                exception.Message);
        }

        [TestCase("-1")]
        [TestCase("999")]
        public void NarrativeValidation_RejectsUndefinedNumericRequirementMode(string mode)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                NarrativeContentJsonImporter.ValidateJson(ObjectivesJson(
                    "objectives",
                    $@"{{
      ""id"":""first"",
      ""title"":""First"",
      ""activationRequirement"":{{""mode"":""{mode}"",""flags"":[""ready""]}}
    }}")));

            StringAssert.Contains(
                "invalid activation requirement mode",
                exception.Message);
        }

        [Test]
        public void NarrativeImport_RejectsGeneratedTargetCollisionBeforeCreatingEitherAsset()
        {
            string collisionPath = root + "/main/main.asset";
            string json = ObjectivesJson(
                "main",
                @"{
      ""id"":""main"",
      ""title"":""Collides with the database""
    }");

            WithTextAsset(json, source =>
            {
                ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                    NarrativeContentJsonImporter.Import(source, root));

                StringAssert.Contains("same Unity asset path", exception.Message);
                Assert.That(AssetDatabase.LoadMainAssetAtPath(collisionPath), Is.Null);
                Assert.That(AssetDatabase.IsValidFolder(root + "/main"), Is.False);
            });
        }

        [Test]
        public void NarrativeImport_RejectsLaterWrongTypeBeforeUpdatingEarlierTarget()
        {
            string outputFolder = root + "/Generated";
            EnsureFolder(outputFolder);
            EnsureFolder(outputFolder + "/main");
            string firstPath = outputFolder + "/main/first.asset";
            string wrongPath = outputFolder + "/main/second.asset";
            ObjectiveDefinition first = CreateObjective(
                firstPath,
                "first",
                "Original title");
            var wrongType = ScriptableObject.CreateInstance<FlagDatabase>();
            AssetDatabase.CreateAsset(wrongType, wrongPath);
            AssetDatabase.SaveAssets();
            string json = ObjectivesJson(
                "main",
                @"{
      ""id"":""first"",
      ""title"":""Mutated title""
    }",
                @"{
      ""id"":""second"",
      ""title"":""Second""
    }");

            WithTextAsset(json, source =>
            {
                ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                    NarrativeContentJsonImporter.Import(source, outputFolder));

                StringAssert.Contains("not ObjectiveDefinition", exception.Message);
                Assert.That(first.Title, Is.EqualTo("Original title"));
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<ObjectiveDefinition>(firstPath),
                    Is.SameAs(first));
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<FlagDatabase>(wrongPath),
                    Is.SameAs(wrongType));
                Assert.That(
                    AssetDatabase.LoadMainAssetAtPath(outputFolder + "/main/main.asset"),
                    Is.Null);
            });
        }

        private static string DialogueJson(string conditionMode = "None") => $@"{{
  ""schemaVersion"":1,
  ""treeId"":""intro"",
  ""startNode"":""start"",
  ""nodes"":[{{
    ""id"":""start"",
    ""speaker"":""Narrator"",
    ""text"":""Begin"",
    ""next"":null,
    ""choices"":[{{
      ""text"":""Continue"",
      ""next"":null,
      ""condition"":{{""mode"":""{conditionMode}"",""flags"":[""ready""]}}
    }}]
  }}]
}}";

        private static string FlagsJson() => @"{
  ""schemaVersion"":1,
  ""contentType"":""flags"",
  ""catalogId"":""story"",
  ""items"":[{""id"":""ready"",""description"":""Ready""}]
}";

        private static string ObjectivesJson(
            string catalogId,
            params string[] items) => $@"{{
  ""schemaVersion"":1,
  ""contentType"":""objectives"",
  ""catalogId"":""{catalogId}"",
  ""items"":[{string.Join(",", items)}]
}}";

        private static ObjectiveDefinition CreateObjective(
            string path,
            string id,
            string title)
        {
            var objective = ScriptableObject.CreateInstance<ObjectiveDefinition>();
            var serialized = new SerializedObject(objective);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("title").stringValue = title;
            serialized.FindProperty("description").stringValue = string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(objective, path);
            return objective;
        }

        private static void EnsureFolder(string path)
        {
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void WithTextAsset(string json, Action<TextAsset> action)
        {
            var source = new TextAsset(json);
            try
            {
                action(source);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }
}
