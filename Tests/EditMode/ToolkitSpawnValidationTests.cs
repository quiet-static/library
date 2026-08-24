using System;
using System.Linq;
using NUnit.Framework;
using QuietStatic.Toolkit.Editor.Validation;
using QuietStatic.Toolkit.Spawning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Tests.EditMode
{
    /// <summary>Protects scene-scoped spawn-point identity validation.</summary>
    public sealed class ToolkitSpawnValidationTests
    {
        [Test]
        public void DuplicateSpawnIds_InDifferentScenes_AreAccepted()
        {
            string firstScenePath = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/ToolkitSpawnValidationScene.unity");
            Scene firstScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            Scene secondScene = default;
            try
            {
                Assert.That(
                    EditorSceneManager.SaveScene(firstScene, firstScenePath),
                    Is.True);
                secondScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive);
                CreateSpawnPoint(firstScene, "Default");
                CreateSpawnPoint(secondScene, "Default");

                Assert.That(DuplicateSpawnIssues(), Is.Empty);
            }
            finally
            {
                if (secondScene.IsValid() && secondScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(secondScene, true);
                }

                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                AssetDatabase.DeleteAsset(firstScenePath);
            }
        }

        [Test]
        public void DuplicateSpawnIds_InTheSameScene_AreRejected()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            try
            {
                SpawnPoint first = CreateSpawnPoint(scene, "Default");
                CreateSpawnPoint(scene, "Default");

                ValidationIssue issue = DuplicateSpawnIssues().Single();
                Assert.That(issue.Severity, Is.EqualTo(ValidationSeverity.Error));
                Assert.That(issue.Context, Is.SameAs(first));
                Assert.That(issue.Message, Does.Contain(scene.name));
                Assert.That(issue.Message, Does.Contain("Default"));
            }
            finally
            {
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
            }
        }

        private static SpawnPoint CreateSpawnPoint(Scene scene, string id)
        {
            var gameObject = new GameObject($"Spawn {id}");
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            SpawnPoint spawnPoint = gameObject.AddComponent<SpawnPoint>();
            var serialized = new SerializedObject(spawnPoint);
            serialized.FindProperty("id").stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return spawnPoint;
        }

        private static ValidationIssue[] DuplicateSpawnIssues() =>
            ToolkitValidation.ScanOpenScenes()
                .Where(issue =>
                    issue.Category == "Spawning" &&
                    issue.Message.IndexOf(
                        "spawn points with ID",
                        StringComparison.Ordinal) >= 0)
                .ToArray();
    }
}
