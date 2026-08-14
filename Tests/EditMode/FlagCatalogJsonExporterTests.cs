using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using QuietStatic.Toolkit.Editor;
using QuietStatic.Toolkit.Editor.Flags;
using QuietStatic.Toolkit.Flags;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class FlagCatalogJsonExporterTests
    {
        [Serializable]
        private sealed class CatalogProbe
        {
            public int schemaVersion;
            public string contentType;
            public string catalogId;
            public ItemProbe[] items;
        }

        [Serializable]
        private sealed class ItemProbe
        {
            public string id;
            public string description;
        }

        private FlagDatabase database;
        private string outputFolder;

        [SetUp]
        public void SetUp()
        {
            database = ScriptableObject.CreateInstance<FlagDatabase>();
            database.name = "stolen_flags";
            outputFolder = Path.Combine(
                Path.GetTempPath(),
                "QuietStaticFlagCatalogExporterTests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (database != null)
                UnityEngine.Object.DestroyImmediate(database);
            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
        }

        [Test]
        public void BuildJson_ProducesAuthorerCompatibleCatalog()
        {
            SetFlags(
                ("door.open", "The service door has been opened."),
                ("met_guard", null));

            string json = FlagCatalogJsonExporter.BuildJson(database);

            Assert.DoesNotThrow(() => NarrativeContentJsonImporter.ValidateJson(json));
            CatalogProbe catalog = JsonUtility.FromJson<CatalogProbe>(json);
            Assert.That(catalog.schemaVersion, Is.EqualTo(1));
            Assert.That(catalog.contentType, Is.EqualTo("flags"));
            Assert.That(catalog.catalogId, Is.EqualTo("stolen_flags"));
            Assert.That(catalog.items.Select(item => item.id),
                Is.EqualTo(new[] { "door.open", "met_guard" }));
            Assert.That(catalog.items[0].description,
                Is.EqualTo("The service door has been opened."));
            Assert.That(catalog.items[1].description, Is.EqualTo(string.Empty));
        }

        [Test]
        public void BuildJson_RejectsIdsWithSurroundingWhitespace()
        {
            SetFlags((" door.open ", "The service door has been opened."));

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                FlagCatalogJsonExporter.BuildJson(database));

            StringAssert.Contains("must not have leading or trailing whitespace", exception.Message);
        }

        [Test]
        public void BuildJson_RejectsDuplicateIds()
        {
            SetFlags(("door.open", "First"), ("door.open", "Second"));

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                FlagCatalogJsonExporter.BuildJson(database));

            StringAssert.Contains("duplicated", exception.Message);
        }

        [Test]
        public void Export_ReplacesExistingFileAndLeavesNoTemporaryFile()
        {
            SetFlags(("story.started", "The story has started."));
            Directory.CreateDirectory(outputFolder);
            string outputPath = Path.Combine(outputFolder, "flags.json");
            File.WriteAllText(outputPath, "old contents");

            string exportedPath = FlagCatalogJsonExporter.Export(
                database,
                outputPath,
                "game_flags");

            Assert.That(exportedPath, Is.EqualTo(Path.GetFullPath(outputPath)));
            string json = File.ReadAllText(outputPath);
            Assert.DoesNotThrow(() => NarrativeContentJsonImporter.ValidateJson(json));
            Assert.That(JsonUtility.FromJson<CatalogProbe>(json).catalogId,
                Is.EqualTo("game_flags"));
            Assert.That(Directory.EnumerateFiles(outputFolder, "*.tmp"), Is.Empty);
            byte[] bytes = File.ReadAllBytes(outputPath);
            Assert.That(bytes.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
        }

        [Test]
        public void Export_RejectsNonJsonDestinationBeforeCreatingFolders()
        {
            SetFlags(("story.started", "The story has started."));
            string outputPath = Path.Combine(outputFolder, "nested", "flags.txt");

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                FlagCatalogJsonExporter.Export(database, outputPath));

            StringAssert.Contains("must end in .json", exception.Message);
            Assert.That(Directory.Exists(outputFolder), Is.False);
        }

        private void SetFlags(params (string id, string description)[] definitions)
        {
            var serialized = new SerializedObject(database);
            SerializedProperty flags = serialized.FindProperty("flags");
            flags.arraySize = definitions.Length;
            for (int index = 0; index < definitions.Length; index++)
            {
                SerializedProperty item = flags.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("id").stringValue = definitions[index].id;
                item.FindPropertyRelative("description").stringValue = definitions[index].description;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
