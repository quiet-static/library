using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Editor.Cinematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class CinematicLaunchOverviewTests
    {
        [SetUp]
        public void SetUp() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [TearDown]
        public void TearDown() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void ExplorerFindsAnimationAndShotReferencesAndFlagsIncompleteSteps()
        {
            var runner = new GameObject("Introduction").AddComponent<CutsceneSequenceRunner>();
            using var data = new SerializedObject(runner);
            SerializedProperty steps = data.FindProperty("steps");
            steps.arraySize = 1;
            SerializedProperty step = steps.GetArrayElementAtIndex(0);
            step.FindPropertyRelative("cameraShotId").stringValue = "office.wide";
            SerializedProperty actions = step.FindPropertyRelative("characterActions");
            actions.arraySize = 1;
            actions.GetArrayElementAtIndex(0).FindPropertyRelative("animatorTrigger").stringValue = "sit";
            data.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(CutsceneExplorerWindow.MatchesSearch(runner, "OFFICE.WIDE"), Is.True);
            Assert.That(CutsceneExplorerWindow.MatchesSearch(runner, "sit"), Is.True);
            Assert.That(CutsceneExplorerWindow.MatchesSearch(runner, "unrelated"), Is.False);
            string[] problems = CutsceneExplorerWindow.GetStepProblems(runner.Steps[0]).ToArray();
            Assert.That(problems.Any(problem => problem.Contains("Camera Director")), Is.True);
            Assert.That(problems.Any(problem => problem.Contains("Character Controller")), Is.True);
        }

        [Test]
        public void DefinitionActivityFindsTheSceneRunnerAndReportsAmbiguousOrMissingBindings()
        {
            var player = new GameObject("Office Cinematics").AddComponent<CinematicScenePlayer>();
            var intro = new GameObject("Intro").AddComponent<CutsceneSequenceRunner>();
            var other = new GameObject("Other runner").AddComponent<CutsceneSequenceRunner>();
            using (var serialized = new SerializedObject(player))
            {
                SerializedProperty activities = serialized.FindProperty("activities");
                activities.arraySize = 2;
                AssignActivity(activities.GetArrayElementAtIndex(0), intro);
                AssignActivity(activities.GetArrayElementAtIndex(1), other);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var definition = ScriptableObject.CreateInstance<CinematicDefinition>();
            try
            {
                var beat = new CinematicDefinition.Beat
                {
                    id = "opening",
                    activityId = "office.intro.sequence",
                    cameraShotId = "office.wide"
                };
                var beats = (System.Collections.Generic.List<CinematicDefinition.Beat>)typeof(CinematicDefinition)
                    .GetField("beats", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(definition);
                beats.Add(beat);

                var rows = CinematicLaunchOverview.ReadBindings(player, definition);
                var activity = rows.Single(row => row.Label.StartsWith("Activity"));
                var camera = rows.Single(row => row.Label.StartsWith("Camera Shot"));
                Assert.That(activity.Target, Is.SameAs(intro));
                Assert.That(activity.Problem, Does.Contain("first match"));
                Assert.That(camera.Target, Is.Null);
                Assert.That(camera.Problem, Does.Contain("exact ID"));

                using var serialized = new SerializedObject(player);
                serialized.FindProperty("activities").arraySize = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                activity = CinematicLaunchOverview.ReadBindings(player, definition).Single(row => row.Target != null);
                Assert.That(activity.Target, Is.SameAs(intro));
                Assert.That(activity.Problem, Is.Null);
            }
            finally { Object.DestroyImmediate(definition); }
        }

        private static void AssignActivity(SerializedProperty binding, CutsceneSequenceRunner runner)
        {
            binding.FindPropertyRelative("id").stringValue = "office.intro.sequence";
            binding.FindPropertyRelative("component").objectReferenceValue = runner;
        }
    }
}
