using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Flags.Editor;
using QuietStatic.Toolkit.Editor;
using QuietStatic.Toolkit.Editor.Flags;
using QuietStatic.Toolkit.Saving;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuietStatic.Tests.EditMode
{
    public sealed class TypedFlagTests
    {
        private readonly List<Object> objects = new();
        private FlagManager manager;
        private FlagManager previous;
        private const string ImportFolder = "Assets/__TypedFlagTests";

        [SetUp] public void SetUp()
        {
            previous = FlagManager.Instance;
            Singleton(null);
        }
        [TearDown] public void TearDown()
        {
            AssetDatabase.DeleteAsset(ImportFolder);
            foreach (var item in objects.AsEnumerable().Reverse()) if (item != null) Object.DestroyImmediate(item);
            objects.Clear();
            FlagIdDrawer.ClearCache();
            Singleton(previous);
        }
        private static void Singleton(FlagManager value) => typeof(FlagManager).BaseType
            .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        private static void Field(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private FlagDatabase Catalog(params FlagDatabase.FlagDefinition[] definitions)
        {
            var database = ScriptableObject.CreateInstance<FlagDatabase>();
            database.name = "NPCFlags";
            objects.Add(database); Field(database, "flags", definitions); return database;
        }
        private void Create(params FlagDatabase[] catalogs)
        {
            var root = new GameObject("Typed flags"); root.SetActive(false); objects.Add(root);
            manager = root.AddComponent<FlagManager>();
            Field(manager, "flagDatabase", catalogs.FirstOrDefault());
            Field(manager, "additionalFlagDatabases", catalogs.Skip(1).ToArray());
            typeof(FlagManager).GetMethod("CacheDatabaseFlags", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
            root.SetActive(true);
        }
        private void CreateRelationship()
        {
            Create(Catalog(new FlagDatabase.FlagDefinition { id = "story.started" }), Catalog(
                new FlagDatabase.FlagDefinition { id = "npc.alex.trust", valueType = FlagValueType.Integer, defaultInteger = 5 },
                new FlagDatabase.FlagDefinition { id = "npc.alex.affection", valueType = FlagValueType.Float, defaultFloat = 1.5f },
                new FlagDatabase.FlagDefinition { id = "npc.alex.nickname", valueType = FlagValueType.String, defaultString = "Alex" },
                new FlagDatabase.FlagDefinition { id = "npc.alex.confided" }));
        }
        [Test] public void MultipleCatalogs_PreserveLegacyBooleanAndResolveTypedDefaults()
        {
            CreateRelationship(); manager.SetFlag("story.started"); manager.SetFlag("npc.alex.confided");
            Assert.That(manager.HasFlag("story.started") && manager.HasFlag("npc.alex.confided"), Is.True);
            Assert.That(manager.Databases.Count(), Is.EqualTo(2));
            manager.TryGetValue("npc.alex.trust", out var trust); Assert.That(trust.Integer, Is.EqualTo(5));
            manager.TryGetValue("npc.alex.affection", out var affection); Assert.That(affection.Float, Is.EqualTo(1.5f));
            manager.TryGetValue("npc.alex.nickname", out var name); Assert.That(name.String, Is.EqualTo("Alex"));
            Assert.That(manager.HasFlag("npc.alex.trust"), Is.False);
        }
        [Test] public void RepeatedCatalogReference_IsNotADuplicateDefinition()
        {
            var catalog = Catalog(new FlagDatabase.FlagDefinition { id = "known" }); Create(catalog, catalog);
            Assert.That(manager.Databases.Count(), Is.EqualTo(1));
        }
        [Test] public void DuplicateIdsAcrossCatalogs_AreRejected()
        {
            var error = Assert.Throws<TargetInvocationException>(() => Create(Catalog(new FlagDatabase.FlagDefinition { id = "same" }), Catalog(new FlagDatabase.FlagDefinition { id = "same" })));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
        }
        [Test] public void SetValue_RejectsWrongTypesUnknownIdsNonfiniteValuesAndIntegerOverflow()
        {
            CreateRelationship();
            Assert.That(manager.SetString("npc.alex.trust", "wrong"), Is.False);
            Assert.That(manager.SetInteger("unknown", 2), Is.False);
            Assert.That(manager.SetFloat("npc.alex.affection", float.NaN), Is.False);
            Assert.That(manager.SetFloat("npc.alex.affection", float.PositiveInfinity), Is.False);
            Assert.That(manager.SetInteger("npc.alex.trust", int.MaxValue), Is.True);
            Assert.That(manager.AddInteger("npc.alex.trust", 1), Is.False);
            manager.TryGetValue("npc.alex.trust", out var value); Assert.That(value.Integer, Is.EqualTo(int.MaxValue));
        }
        [Test] public void TypedMutations_NotifyOnceOnlyOnChangeAndLeaveDefinitionsUntouched()
        {
            CreateRelationship(); int changed = 0; manager.FlagsChanged += () => changed++;
            Assert.That(manager.AddInteger("npc.alex.trust", 3), Is.True);
            Assert.That(manager.SetInteger("npc.alex.trust", 8), Is.True);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(manager.Databases.Last().Flags[0].defaultInteger, Is.EqualTo(5));
            manager.SetString("npc.alex.nickname", null);
            manager.TryGetValue("npc.alex.nickname", out var name); Assert.That(name.String, Is.Empty);
        }
        [Test] public void BooleanMethods_DoNotCoerceNumericFlags()
        {
            CreateRelationship(); manager.SetFlag("npc.alex.trust"); manager.ClearFlag("npc.alex.trust");
            manager.TryGetValue("npc.alex.trust", out var value); Assert.That(value.Integer, Is.EqualTo(5));
            Assert.That(manager.ActiveFlags, Does.Not.Contain("npc.alex.trust"));
        }
        [TestCase(FlagComparisonOperator.Greater, 4, true)]
        [TestCase(FlagComparisonOperator.GreaterOrEqual, 5, true)]
        [TestCase(FlagComparisonOperator.Less, 5, false)]
        [TestCase(FlagComparisonOperator.LessOrEqual, 5, true)]
        [TestCase(FlagComparisonOperator.Equal, 5, true)]
        [TestCase(FlagComparisonOperator.NotEqual, 5, false)]
        public void Comparisons_UseExactTypedNumericSemantics(FlagComparisonOperator comparison, int expected, bool result)
        {
            CreateRelationship();
            var condition = new FlagComparison { flagId = "npc.alex.trust", comparison = comparison, expected = FlagValue.From(expected) };
            Assert.That(condition.IsMet(manager), Is.EqualTo(result));
            condition.expected = FlagValue.From((float)expected); Assert.That(condition.IsMet(manager), Is.False);
        }
        [Test] public void Requirement_CombinesBooleanGateAndTypedComparison()
        {
            CreateRelationship();
            var requirement = new FlagRequirement(FlagRequirementMode.All, new[] { "story.started" }, new[] {
                new FlagComparison { flagId = "npc.alex.trust", comparison = FlagComparisonOperator.GreaterOrEqual, expected = FlagValue.From(8) }
            });
            Assert.That(requirement.IsConfigured, Is.True); Assert.That(requirement.IsMet(manager), Is.False);
            manager.SetFlag("story.started"); Assert.That(requirement.IsMet(manager), Is.False);
            manager.AddInteger("npc.alex.trust", 3); Assert.That(requirement.IsMet(manager), Is.True);
        }
        [Test] public void Snapshot_RoundTripsThroughSaveJsonAndRestoresEveryKind()
        {
            CreateRelationship(); manager.SetFlag("story.started"); manager.SetInteger("npc.alex.trust", 42);
            manager.SetFloat("npc.alex.affection", 3.75f); manager.SetString("npc.alex.nickname", "A");
            var data = new SaveGameData { activeFlags = manager.ActiveFlags.ToList(), flagValues = manager.CaptureValues() };
            var restored = JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(data));
            manager.ClearAllFlags(); manager.RestoreValues(restored.activeFlags, restored.flagValues);
            Assert.That(manager.HasFlag("story.started"), Is.True);
            manager.TryGetValue("npc.alex.trust", out var trust); Assert.That(trust.Integer, Is.EqualTo(42));
            manager.TryGetValue("npc.alex.affection", out var affection); Assert.That(affection.Float, Is.EqualTo(3.75f));
            manager.TryGetValue("npc.alex.nickname", out var name); Assert.That(name.String, Is.EqualTo("A"));
        }
        [Test] public void LegacyRestore_UsesTypedDefaultsAndOnlySavedBooleanFlags()
        {
            CreateRelationship(); manager.SetInteger("npc.alex.trust", 99);
            manager.RestoreValues(new[] { "npc.alex.confided" }, null);
            Assert.That(manager.HasFlag("npc.alex.confided"), Is.True);
            manager.TryGetValue("npc.alex.trust", out var trust); Assert.That(trust.Integer, Is.EqualTo(5));
        }
        [Test] public void InvalidRestore_IsAtomic()
        {
            CreateRelationship(); manager.SetInteger("npc.alex.trust", 8);
            Assert.Throws<ArgumentException>(() => manager.RestoreValues(null, new[] {
                new FlagValueEntry("npc.alex.trust", FlagValue.From("wrong")) }));
            manager.TryGetValue("npc.alex.trust", out var trust); Assert.That(trust.Integer, Is.EqualTo(8));
        }
        [Test] public void CatalogJson_RoundTripsTypesAndDefaults()
        {
            var catalog = Catalog(new FlagDatabase.FlagDefinition { id = "npc.trust", valueType = FlagValueType.Integer, defaultInteger = 12 });
            var json = FlagCatalogJsonExporter.BuildJson(catalog);
            var source = new TextAsset(json); objects.Add(source);
            var imported = (FlagDatabase)NarrativeContentJsonImporter.Import(source, ImportFolder);
            Assert.That(imported.Flags[0].valueType, Is.EqualTo(FlagValueType.Integer));
            Assert.That(imported.Flags[0].defaultInteger, Is.EqualTo(12));
        }
        [Test] public void DialogueJson_PreservesTypedChoiceConditionsAndPreview()
        {
            var tree = ScriptableObject.CreateInstance<QuietStatic.Toolkit.Dialogue.DialogueTree>();
            objects.Add(tree); tree.name = "typed-dialogue";
            var requirement = new FlagRequirement(FlagRequirementMode.None, null, new[] {
                new FlagComparison { flagId = "npc.trust", comparison = FlagComparisonOperator.GreaterOrEqual, expected = FlagValue.From(10) }
            });
            Field(tree, "nodes", new[] { new QuietStatic.Toolkit.Dialogue.DialogueTree.Node {
                id = "start", speaker = "Alex", line = "Can I trust you?", nextNodeIndex = -1,
                choices = new[] { new QuietStatic.Toolkit.Dialogue.DialogueTree.Choice {
                    text = "Tell the truth", nextNodeIndex = -1, availabilityRequirement = requirement }
                }
            }});
            string json = QuietStatic.Toolkit.Editor.Dialogue.DialogueJsonExporter.BuildJson(tree);
            var source = new TextAsset(json); objects.Add(source);
            var restored = QuietStatic.Toolkit.Editor.Dialogue.DialogueJsonImporter.Import(source, ImportFolder);
            var condition = restored.Nodes[0].choices[0].availabilityRequirement.Comparisons.Single();
            Assert.That(condition.expected.Type, Is.EqualTo(FlagValueType.Integer));
            Assert.That(condition.expected.Integer, Is.EqualTo(10));
            Assert.That(QuietStatic.Toolkit.Editor.Dialogue.DialoguePathPreview.AvailableChoiceIndexes(restored.Nodes[0], null), Is.Empty);
            Assert.That(QuietStatic.Toolkit.Editor.Dialogue.DialoguePathPreview.AvailableChoiceIndexes(restored.Nodes[0], null,
                new Dictionary<string, FlagValue> { ["npc.trust"] = FlagValue.From(12) }), Is.EqualTo(new[] { 0 }));
        }
        [Test] public void SaveReader_MigratesVersionOneExplicitly()
        {
            var root = new GameObject("Save manager"); objects.Add(root);
            var save = root.AddComponent<SaveManager>();
            string path = System.IO.Path.GetTempFileName();
            try
            {
                System.IO.File.WriteAllText(path, "{\"version\":1,\"activeFlags\":[\"story.started\"]}");
                object[] arguments = { path, null, null };
                bool read = (bool)typeof(SaveManager).GetMethod("TryDeserializeFile", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(save, arguments);
                Assert.That(read, Is.True, arguments[2] as string);
                var data = (SaveGameData)arguments[1];
                Assert.That(data.version, Is.EqualTo(2));
                Assert.That(data.activeFlags, Is.EqualTo(new[] { "story.started" }));
                Assert.That(data.flagValues, Is.Empty);
            }
            finally { System.IO.File.Delete(path); }
        }
        [Test] public void Picker_CollectsIdsFromEveryCatalog()
        {
            AssetDatabase.CreateFolder("Assets", "__TypedFlagTests");
            var first = Catalog(new FlagDatabase.FlagDefinition { id = "test.npc.first" }); var second = Catalog(new FlagDatabase.FlagDefinition { id = "test.npc.second" });
            AssetDatabase.CreateAsset(first, ImportFolder + "/First.asset");
            AssetDatabase.CreateAsset(second, ImportFolder + "/Second.asset");
            FlagIdDrawer.ClearCache();
            typeof(FlagIdDrawer).GetMethod("EnsureCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            var ids = (string[])typeof(FlagIdDrawer).GetField("cachedIds", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Assert.That(ids, Does.Contain("test.npc.first")); Assert.That(ids, Does.Contain("test.npc.second"));
        }
    }
}
