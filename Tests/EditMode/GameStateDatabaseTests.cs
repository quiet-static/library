using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Core;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class GameStateDatabaseTests
    {
        private GameStateDatabase database;

        [SetUp]
        public void SetUp()
        {
            database = ScriptableObject.CreateInstance<GameStateDatabase>();
            typeof(GameStateDatabase)
                .GetField("states", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(
                    database,
                    new[]
                    {
                        new GameStateDatabase.StateDefinition
                        {
                            state = " Playing ",
                            description = " Normal gameplay. "
                        },
                        new GameStateDatabase.StateDefinition
                        {
                            state = "Paused",
                            description = "Pause menu"
                        }
                    });
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(database);
        }

        [Test]
        public void Contains_NormalizesConfiguredAndRequestedIds()
        {
            Assert.That(database.Contains("Playing"), Is.True);
            Assert.That(database.Contains(" Paused "), Is.True);
            Assert.That(database.Contains("Missing"), Is.False);
        }

        [Test]
        public void GetDescription_ReturnsTrimmedDocumentation()
        {
            Assert.That(
                database.GetDescription("Playing"),
                Is.EqualTo("Normal gameplay."));
            Assert.That(database.GetDescription("Missing"), Is.Empty);
        }

        [Test]
        public void ProjectValidation_HasNoGameStateErrors()
        {
            Type validationType = Type.GetType(
                "QuietStatic.Toolkit.Editor.Validation.ToolkitValidation, " +
                "QuietStatic.Core.Editor");
            Assert.That(validationType, Is.Not.Null);

            MethodInfo scan = validationType.GetMethod(
                "ScanOpenScenes",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(scan, Is.Not.Null);

            var messages = new List<string>();
            foreach (object issue in (IEnumerable)scan.Invoke(null, null))
            {
                Type issueType = issue.GetType();
                string category = (string)issueType
                    .GetProperty("Category")
                    ?.GetValue(issue);
                string severity = issueType
                    .GetProperty("Severity")
                    ?.GetValue(issue)
                    ?.ToString();

                if (category == "Game States" && severity == "Error")
                {
                    messages.Add((string)issueType
                        .GetProperty("Message")
                        ?.GetValue(issue));
                }
            }

            Assert.That(messages, Is.Empty);
        }
    }
}
