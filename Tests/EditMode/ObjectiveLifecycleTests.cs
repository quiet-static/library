using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Objectives;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class ObjectiveLifecycleTests
    {
        private GameObject objectiveManagerObject;
        private GameObject flagManagerObject;
        private ObjectiveManager objectiveManager;
        private FlagManager flagManager;

        [TearDown]
        public void TearDown()
        {
            if (objectiveManagerObject != null)
            {
                Object.DestroyImmediate(objectiveManagerObject);
            }

            if (flagManagerObject != null)
            {
                Object.DestroyImmediate(flagManagerObject);
            }
        }

        [Test]
        public void Database_ResolvesDefinitionsByStableId()
        {
            ObjectiveDefinition objective =
                CreateObjective("house.find-key", "Find the key");
            ObjectiveDatabase database =
                ScriptableObject.CreateInstance<ObjectiveDatabase>();
            SetPrivateField(
                database,
                "objectives",
                new[] { objective });

            Assert.That(
                database.FindById("house.find-key"),
                Is.SameAs(objective));
            Assert.That(database.Contains(objective), Is.True);
            Assert.That(database.FindById("missing"), Is.Null);

            Object.DestroyImmediate(database);
            Object.DestroyImmediate(objective);
        }

        [Test]
        public void Lifecycle_RaisesEventsAndRestoresSavedState()
        {
            ObjectiveDefinition completed =
                CreateObjective("house.find-key", "Find the key");
            ObjectiveDefinition active =
                CreateObjective("house.open-door", "Open the door");
            ObjectiveDatabase database =
                ScriptableObject.CreateInstance<ObjectiveDatabase>();
            SetPrivateField(
                database,
                "objectives",
                new[] { completed, active });
            CreateObjectiveManager(database);

            int activatedCount = 0;
            int completedCount = 0;
            System.Action<ObjectiveDefinition> activatedHandler =
                _ => activatedCount++;
            System.Action<ObjectiveDefinition> completedHandler =
                _ => completedCount++;
            ObjectiveManager.OnObjectiveActivated += activatedHandler;
            ObjectiveManager.OnObjectiveCompleted += completedHandler;

            try
            {
                Assert.That(
                    objectiveManager.ActivateObjective(completed),
                    Is.True);
                Assert.That(
                    objectiveManager.CompleteActiveObjective(),
                    Is.True);
                Assert.That(
                    objectiveManager.ActivateObjective(completed),
                    Is.False);
                Assert.That(
                    objectiveManager.ActivateObjective(active),
                    Is.True);

                string json = objectiveManager.CaptureSaveState();
                objectiveManager.ClearActiveObjective();
                objectiveManager.RestoreSaveState(json);

                Assert.That(activatedCount, Is.EqualTo(2));
                Assert.That(completedCount, Is.EqualTo(1));
                Assert.That(
                    objectiveManager.ActiveObjective,
                    Is.SameAs(active));
                Assert.That(
                    objectiveManager.HasCompleted(completed),
                    Is.True);
                Assert.That(
                    objectiveManager.SaveId,
                    Is.EqualTo("quietstatic.objectives"));
            }
            finally
            {
                ObjectiveManager.OnObjectiveActivated -= activatedHandler;
                ObjectiveManager.OnObjectiveCompleted -= completedHandler;
                Object.DestroyImmediate(database);
                Object.DestroyImmediate(completed);
                Object.DestroyImmediate(active);
            }
        }

        [Test]
        public void ActiveObjective_CompletesWhenFlagRequirementIsEvaluated()
        {
            ObjectiveDefinition objective =
                CreateObjective("house.unlock-door", "Unlock the door");
            var requirement = new FlagRequirement();
            SetPrivateField(
                requirement,
                "mode",
                FlagRequirementMode.All);
            SetPrivateField(
                requirement,
                "flags",
                new[] { "door.unlocked" });
            SetPrivateField(
                objective,
                "completionRequirement",
                requirement);

            CreateObjectiveManager();
            flagManagerObject = new GameObject("Flag Manager");
            flagManager = flagManagerObject.AddComponent<FlagManager>();

            Assert.That(
                objectiveManager.ActivateObjective(objective),
                Is.True);
            Assert.That(
                objectiveManager.ActiveObjective,
                Is.SameAs(objective));

            flagManager.SetFlag("door.unlocked");
            Assert.That(
                objective.IsCompletionMet(flagManager),
                Is.True);
            objectiveManager.EvaluateActiveObjectiveCompletion(flagManager);

            Assert.That(objectiveManager.ActiveObjective, Is.Null);
            Assert.That(
                objectiveManager.HasCompleted(objective),
                Is.True);

            Object.DestroyImmediate(objective);
        }

        private void CreateObjectiveManager(
            ObjectiveDatabase database = null)
        {
            objectiveManagerObject = new GameObject("Objective Manager");
            objectiveManager =
                objectiveManagerObject.AddComponent<ObjectiveManager>();
            SetPrivateField(objectiveManager, "database", database);
        }

        private static ObjectiveDefinition CreateObjective(
            string id,
            string title)
        {
            ObjectiveDefinition objective =
                ScriptableObject.CreateInstance<ObjectiveDefinition>();
            objective.name = title;
            SetPrivateField(objective, "id", id);
            SetPrivateField(objective, "title", title);
            return objective;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
