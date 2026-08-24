using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.EditMode
{
    public sealed class FlagManagerTests
    {
        private GameObject managerObject;
        private FlagManager manager;
        private FlagDatabase database;
        private FlagManager previousInstance;
        private Action<string> flagSetHandler;
        private Action flagsChangedHandler;

        [SetUp]
        public void SetUp()
        {
            previousInstance = FlagManager.Instance;
            SetSingletonInstance(null);
        }

        [TearDown]
        public void TearDown()
        {
            if (manager != null)
            {
                manager.FlagSet -= flagSetHandler;
                manager.FlagsChanged -= flagsChangedHandler;
            }
            flagSetHandler = null;
            flagsChangedHandler = null;

            if (managerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }

            if (database != null)
            {
                UnityEngine.Object.DestroyImmediate(database);
            }

            SetSingletonInstance(previousInstance);
            previousInstance = null;
        }

        [Test]
        public void SetFlag_NormalizesAndIsIdempotent()
        {
            CreateManager();
            var setFlags = new List<string>();
            int changedCount = 0;
            flagSetHandler = setFlags.Add;
            flagsChangedHandler = () => changedCount++;
            manager.FlagSet += flagSetHandler;
            manager.FlagsChanged += flagsChangedHandler;

            manager.SetFlag("  clue.found  ");
            manager.SetFlag("clue.found");

            Assert.That(manager.ActiveFlags, Is.EqualTo(new[] { "clue.found" }));
            Assert.That(setFlags, Is.EqualTo(new[] { "clue.found" }));
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void Database_RejectsUnknownIds()
        {
            database = ScriptableObject.CreateInstance<FlagDatabase>();
            SetField(database, "flags", new[]
            {
                new FlagDatabase.FlagDefinition { id = "  known  " },
                null,
                new FlagDatabase.FlagDefinition { id = "   " },
            });
            CreateManager(database);

            manager.SetFlag(" known ");
            LogAssert.Expect(
                LogType.Warning,
                "[Warning] AddFlagSilently: [FlagManager] Cannot set " +
                "unknown flag 'unknown'. Add it to the assigned FlagDatabase first.");
            manager.SetFlag("unknown");

            Assert.That(manager.HasFlag("known"), Is.True);
            Assert.That(manager.HasFlag("unknown"), Is.False);
            Assert.That(manager.IsKnownFlag("known"), Is.True);
            Assert.That(manager.IsKnownFlag("unknown"), Is.False);
        }

        [Test]
        public void Dependencies_CascadeUntilStable()
        {
            FlagManager.FlagDependency second = CreateDependency(
                "complete",
                "middle");
            FlagManager.FlagDependency first = CreateDependency(
                "middle",
                "start");
            CreateManager(dependencies: new[] { second, first });
            var setFlags = new List<string>();
            flagSetHandler = setFlags.Add;
            manager.FlagSet += flagSetHandler;

            manager.SetFlag("start");

            Assert.That(
                manager.ActiveFlags,
                Is.EquivalentTo(new[] { "start", "middle", "complete" }));
            Assert.That(
                setFlags,
                Is.EqualTo(new[] { "start", "middle", "complete" }));

            manager.SetFlag("start");
            Assert.That(setFlags, Has.Count.EqualTo(3));
        }

        [Test]
        public void RestoreFlags_NormalizesFiltersAndAppliesDependencies()
        {
            database = ScriptableObject.CreateInstance<FlagDatabase>();
            SetField(database, "flags", new[]
            {
                new FlagDatabase.FlagDefinition { id = "start" },
                new FlagDatabase.FlagDefinition { id = "complete" },
            });
            CreateManager(
                database,
                new[] { CreateDependency("complete", "start") });
            var setFlags = new List<string>();
            flagSetHandler = setFlags.Add;
            manager.FlagSet += flagSetHandler;

            LogAssert.Expect(
                LogType.Warning,
                "[Warning] AddFlagSilently: [FlagManager] Cannot set " +
                "unknown flag 'unknown'. Add it to the assigned FlagDatabase first.");
            manager.RestoreFlags(new[] { " start ", "start", "unknown", "" });

            Assert.That(
                manager.ActiveFlags,
                Is.EquivalentTo(new[] { "start", "complete" }));
            Assert.That(setFlags, Is.EqualTo(new[] { "complete" }));
        }

        private void CreateManager(
            FlagDatabase configuredDatabase = null,
            FlagManager.FlagDependency[] dependencies = null)
        {
            managerObject = new GameObject("Flags");
            managerObject.SetActive(false);
            manager = managerObject.AddComponent<FlagManager>();
            SetField(manager, "flagDatabase", configuredDatabase);
            SetField(manager, "dependencies", dependencies);
            InvokeMethod(manager, "CacheDatabaseFlags");
            managerObject.SetActive(true);
        }

        private static FlagManager.FlagDependency CreateDependency(
            string result,
            params string[] requirements)
        {
            var dependency = new FlagManager.FlagDependency();
            SetField(dependency, "resultFlag", result);
            SetField(dependency, "requiredFlags", requirements);
            return dependency;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }

        private static void InvokeMethod(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method '{name}'.");
            method.Invoke(target, null);
        }

        private static void SetSingletonInstance(FlagManager value)
        {
            FieldInfo field = typeof(FlagManager).BaseType?.GetField(
                "<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing singleton backing field.");
            field.SetValue(null, value);
        }
    }
}
