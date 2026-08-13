using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Editor.Cinematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Tests.EditMode
{
    /// <summary>Protects stable shot references and undoable edit-mode camera placement.</summary>
    public sealed class CinematicShotEditorTests
    {
        private readonly List<Object> createdObjects = new();
        private Scene previousActiveScene;
        private Scene testScene;
        private string testSceneAssetPath;
        private bool replacedBatchModeScene;

        [SetUp]
        public void SetUp()
        {
            previousActiveScene = SceneManager.GetActiveScene();
            replacedBatchModeScene =
                Application.isBatchMode &&
                string.IsNullOrEmpty(previousActiveScene.path);
            testScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                replacedBatchModeScene
                    ? NewSceneMode.Single
                    : NewSceneMode.Additive);
            SceneManager.SetActiveScene(testScene);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    Object.DestroyImmediate(createdObjects[index]);
                }
            }
            createdObjects.Clear();

            if (replacedBatchModeScene)
            {
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
            }
            else if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
                if (testScene.IsValid() && testScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(testScene, true);
                }
            }
            if (!string.IsNullOrEmpty(testSceneAssetPath))
            {
                AssetDatabase.DeleteAsset(testSceneAssetPath);
                testSceneAssetPath = null;
            }
        }

        [Test]
        public void StableShotIdResolvesAfterShotListIsReordered()
        {
            CinematicCutsceneCameraDirector director = CreateDirector(
                out Camera camera,
                out Transform wideMarker,
                out Transform closeMarker);
            camera.fieldOfView = 60f;
            CinematicCutsceneCameraDirector.CinematicShot wideShot =
                CreateShot("shot.wide", "Wide", wideMarker, 50f);
            CinematicCutsceneCameraDirector.CinematicShot closeShot =
                CreateShot("shot.close", "Close", closeMarker, 35f);
            SetShots(director, new List<CinematicCutsceneCameraDirector.CinematicShot>
            {
                wideShot,
                closeShot,
            });
            Assert.That(director.TryGetShotIndex("shot.wide", out int originalIndex), Is.True);
            Assert.That(originalIndex, Is.Zero);

            SetShots(director, new List<CinematicCutsceneCameraDirector.CinematicShot>
            {
                closeShot,
                wideShot,
            });

            director.CutToShot("shot.wide");

            Assert.That(director.CurrentShotIndex, Is.EqualTo(1));
            Assert.That(director.CurrentShotId, Is.EqualTo("shot.wide"));
            Assert.That(director.transform.position, Is.EqualTo(wideMarker.position));
            Assert.That(
                Quaternion.Angle(director.transform.rotation, wideMarker.rotation),
                Is.LessThan(0.001f));
            Assert.That(camera.fieldOfView, Is.EqualTo(50f));
        }

        [Test]
        public void EditorPreviewMovesCameraAndUndoRestoresPoseAndLens()
        {
            CinematicCutsceneCameraDirector director = CreateDirector(
                out Camera camera,
                out Transform wideMarker,
                out _);
            Vector3 originalPosition = new(-4f, 2f, 8f);
            Quaternion originalRotation = Quaternion.Euler(2f, 20f, 0f);
            director.transform.SetPositionAndRotation(
                originalPosition,
                originalRotation);
            camera.fieldOfView = 62f;
            SetShots(director, new List<CinematicCutsceneCameraDirector.CinematicShot>
            {
                CreateShot("shot.wide", "Wide", wideMarker, 44f),
            });
            SaveTestSceneClean();

            Assert.That(testScene.isDirty, Is.False);
            Assert.That(director.CurrentShotIndex, Is.EqualTo(-1));
            Assert.That(
                CinematicShotPreviewUtility.MoveCameraToShot(
                    director,
                    "shot.wide"),
                Is.True);
            Assert.That(director.transform.position, Is.EqualTo(wideMarker.position));
            Assert.That(camera.fieldOfView, Is.EqualTo(44f));
            Assert.That(testScene.isDirty, Is.True);
            Assert.That(director.CurrentShotIndex, Is.EqualTo(-1));

            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();

            Assert.That(director.transform.position, Is.EqualTo(originalPosition));
            Assert.That(
                Quaternion.Angle(director.transform.rotation, originalRotation),
                Is.LessThan(0.001f));
            Assert.That(camera.fieldOfView, Is.EqualTo(62f));
            Assert.That(director.CurrentShotIndex, Is.EqualTo(-1));
        }

        [Test]
        public void EditorPreviewRejectsUnusableShotWithoutDirtyingScene()
        {
            CinematicCutsceneCameraDirector director = CreateDirector(
                out Camera camera,
                out _,
                out _);
            Vector3 originalPosition = director.transform.position;
            float originalFieldOfView = camera.fieldOfView;
            SetShots(director, new List<CinematicCutsceneCameraDirector.CinematicShot>
            {
                new()
                {
                    shotId = "shot.incomplete",
                    shotName = "Incomplete",
                    changeFieldOfView = true,
                    fieldOfView = 20f,
                },
            });
            SaveTestSceneClean();

            Assert.That(
                CinematicShotPreviewUtility.MoveCameraToShot(
                    director,
                    "shot.incomplete"),
                Is.False);
            Assert.That(testScene.isDirty, Is.False);
            Assert.That(director.transform.position, Is.EqualTo(originalPosition));
            Assert.That(camera.fieldOfView, Is.EqualTo(originalFieldOfView));
        }

        [Test]
        public void ExplicitShotIdTakesPrecedenceOverLegacyShotName()
        {
            CinematicCutsceneCameraDirector director = CreateDirector(
                out _,
                out Transform legacyMarker,
                out Transform explicitMarker);
            SetShots(director, new List<CinematicCutsceneCameraDirector.CinematicShot>
            {
                CreateShot(string.Empty, "shot.shared", legacyMarker, 40f),
                CreateShot("shot.shared", "Explicit", explicitMarker, 40f),
            });

            Assert.That(director.TryGetShotIndex("shot.shared", out int shotIndex), Is.True);
            Assert.That(shotIndex, Is.EqualTo(1));

            director.CutToShot("shot.shared");

            Assert.That(director.transform.position, Is.EqualTo(explicitMarker.position));
        }

        [Test]
        public void ShotDrawerFindsDirectorBesideNestedStepAndCueFields()
        {
            GameObject runnerObject = Track(new GameObject("Sequence Runner"));
            CutsceneSequenceRunner runner =
                runnerObject.AddComponent<CutsceneSequenceRunner>();
            SetField(runner, "steps", new[] { new CutsceneSequenceRunner.Step() });
            SerializedProperty stepShot = new SerializedObject(runner)
                .FindProperty("steps")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("cameraShotId");

            Assert.That(
                CinematicShotIdDrawer.FindSibling(stepShot, "cameraDirector")
                    .propertyPath,
                Is.EqualTo("steps.Array.data[0].cameraDirector"));

            GameObject cueObject = Track(new GameObject("Dialogue Cues"));
            DialogueNodeCinematicCue cues =
                cueObject.AddComponent<DialogueNodeCinematicCue>();
            SetField(cues, "cues", new List<DialogueNodeCinematicCue.Cue>
            {
                new(),
            });
            SerializedProperty cueShot = new SerializedObject(cues)
                .FindProperty("cues")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("cameraShotId");

            Assert.That(
                CinematicShotIdDrawer.FindSibling(cueShot, "shotIndex")
                    .propertyPath,
                Is.EqualTo("cues.Array.data[0].shotIndex"));
        }

        private CinematicCutsceneCameraDirector CreateDirector(
            out Camera camera,
            out Transform wideMarker,
            out Transform closeMarker)
        {
            GameObject cameraObject = Track(new GameObject("Cinematic Camera"));
            camera = cameraObject.AddComponent<Camera>();
            CinematicCutsceneCameraDirector director =
                cameraObject.AddComponent<CinematicCutsceneCameraDirector>();
            SetField(director, "cutsceneCamera", null);

            wideMarker = Track(new GameObject("Wide Marker")).transform;
            wideMarker.SetPositionAndRotation(
                new Vector3(1f, 2f, 3f),
                Quaternion.Euler(10f, 25f, 0f));
            closeMarker = Track(new GameObject("Close Marker")).transform;
            closeMarker.SetPositionAndRotation(
                new Vector3(4f, 5f, 6f),
                Quaternion.Euler(5f, 90f, 0f));
            return director;
        }

        private static CinematicCutsceneCameraDirector.CinematicShot CreateShot(
            string id,
            string name,
            Transform marker,
            float fieldOfView)
        {
            return new CinematicCutsceneCameraDirector.CinematicShot
            {
                shotId = id,
                shotName = name,
                cameraPositionMarker = marker,
                changeFieldOfView = true,
                fieldOfView = fieldOfView,
            };
        }

        private static void SetShots(
            CinematicCutsceneCameraDirector director,
            List<CinematicCutsceneCameraDirector.CinematicShot> shots)
        {
            SetField(director, "shots", shots);
        }

        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }

        private void SaveTestSceneClean()
        {
            testSceneAssetPath = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/__QuietStaticCinematicShotEditorTests.unity");
            Assert.That(
                EditorSceneManager.SaveScene(testScene, testSceneAssetPath, false),
                Is.True);
            Assert.That(testScene.isDirty, Is.False);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }
    }
}
