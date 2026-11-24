using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Tooling;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Horror;
using QuietStatic.Toolkit.Objectives;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Tests.EditMode
{
    public sealed class ContentIdChangeServiceTests
    {
        private const string OldId = "test.content.rename.old";
        private const string NewId = "test.content.rename.new";
        private string folder;
        private FlagDatabase source;
        private Scene originalActiveScene;
        private bool replacedBatchModeScene;

        [SetUp]
        public void SetUp()
        {
            originalActiveScene = SceneManager.GetActiveScene();
            string name = "ContentIdChangeTests-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", name);
            folder = $"Assets/{name}";
            replacedBatchModeScene = Application.isBatchMode && string.IsNullOrEmpty(originalActiveScene.path);
            if (replacedBatchModeScene)
            {
                Scene anchor = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Assert.That(EditorSceneManager.SaveScene(anchor, $"{folder}/Anchor.unity"), Is.True);
            }
            source = ScriptableObject.CreateInstance<FlagDatabase>();
            using var data = new SerializedObject(source);
            SerializedProperty flags = data.FindProperty("flags");
            flags.arraySize = 1;
            flags.GetArrayElementAtIndex(0).FindPropertyRelative("id").stringValue = OldId;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(source, $"{folder}/Flags.asset");
        }

        [TearDown]
        public void TearDown()
        {
            if (replacedBatchModeScene)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
                SceneManager.SetActiveScene(originalActiveScene);
            foreach (SceneSetup setup in EditorSceneManager.GetSceneManagerSetup())
            {
                if (!setup.path.StartsWith(folder + "/", StringComparison.Ordinal)) continue;
                Scene scene = SceneManager.GetSceneByPath(setup.path);
                if (scene.IsValid()) EditorSceneManager.CloseScene(scene, removeScene: true);
            }
            AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void PreviewReadsDirtyOpenAndClosedScenesWithoutChangingTheSetup()
        {
            CreateScene("Closed", OldId, keepOpen: false);
            var (openScene, openHandler) = CreateScene("Open", "on-disk", keepOpen: true);
            SetFlag(openHandler, OldId);
            Assert.That(openScene.isDirty, Is.True);
            SceneManager.SetActiveScene(openScene);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(openScene));
            var before = CaptureSceneSetup();
            int previewCount = EditorSceneManager.previewSceneCount;

            ContentIdChangePreview preview = Preview();

            Assert.That(preview.References.Select(edit => edit.AssetPath), Is.EquivalentTo(
                new[] { $"{folder}/Closed.unity", openScene.path }));
            Assert.That(CaptureSceneSetup(), Is.EqualTo(before));
            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewCount));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(openScene));
            Assert.That(openScene.isDirty, Is.True);
            Assert.That(ReadFlag(openHandler), Is.EqualTo(OldId));
            var exception = Assert.Throws<InvalidOperationException>(() => ContentIdChangeService.Apply(preview));
            Assert.That(exception.Message, Does.Contain("Save the affected scene"));
            Assert.That(source.Flags[0].id, Is.EqualTo(OldId));
            Assert.That(ReadSavedSceneFlag("Closed"), Is.EqualTo(OldId));
        }

        [Test]
        public void AddingAConsumerAfterPreviewRejectsTheWholeRename()
        {
            var (_, reviewed) = CreateScene("Reviewed", OldId, keepOpen: true);
            ContentIdChangePreview preview = Preview();
            CreateScene("Added", OldId, keepOpen: false);

            var exception = Assert.Throws<InvalidOperationException>(() => ContentIdChangeService.Apply(preview));

            Assert.That(exception.Message, Does.Contain("Consumer references changed"));
            Assert.That(source.Flags[0].id, Is.EqualTo(OldId));
            Assert.That(ReadFlag(reviewed), Is.EqualTo(OldId));
            Assert.That(ReadSavedSceneFlag("Added"), Is.EqualTo(OldId));
        }

        [Test]
        public void ReplacingAConsumerWithAnIdenticallyNamedObjectRejectsTheRename()
        {
            var (scene, handler) = CreateScene("Reviewed", OldId, keepOpen: true);
            ContentIdChangePreview preview = Preview();
            // Save the replacement while the original is still present so Unity assigns a different file ID.
            FlagHandler added = CreateHandler(scene, handler.gameObject.name);
            SetFlag(added, OldId);
            Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            UnityEngine.Object.DestroyImmediate(handler.gameObject);
            Assert.That(EditorSceneManager.SaveScene(scene), Is.True);

            var exception = Assert.Throws<InvalidOperationException>(() => ContentIdChangeService.Apply(preview));

            Assert.That(exception.Message, Does.Contain("Consumer references changed"));
            Assert.That(source.Flags[0].id, Is.EqualTo(OldId));
            Assert.That(ReadFlag(added), Is.EqualTo(OldId));
        }

        [Test]
        public void ApplyingAReviewedRenameUpdatesClosedAndOpenScenes()
        {
            CreateScene("Closed", OldId, keepOpen: false);
            CreateScene("Unloaded", OldId, keepOpen: false);
            EditorSceneManager.OpenScene($"{folder}/Unloaded.unity", OpenSceneMode.AdditiveWithoutLoading);
            var (_, openHandler) = CreateScene("Open", OldId, keepOpen: true);
            var before = CaptureSceneSetup();
            ContentIdChangePreview preview = Preview();

            ContentIdChangeService.Apply(preview);

            Assert.That(source.Flags[0].id, Is.EqualTo(NewId));
            Assert.That(ReadFlag(openHandler), Is.EqualTo(NewId));
            Assert.That(ReadSavedSceneFlag("Closed"), Is.EqualTo(NewId));
            Assert.That(ReadSavedSceneFlag("Unloaded"), Is.EqualTo(NewId));
            Assert.That(CaptureSceneSetup(), Is.EqualTo(before));
        }

        [Test]
        public void RenameSavesReviewedAssetAndRejectsSubsequentUseOfThePreview()
        {
            string treePath = $"{folder}/Dialogue.asset";
            CreateDialogueAsset(treePath);
            ContentIdChangePreview preview = Preview();

            ContentIdChangeService.Apply(preview);

            var saved = AssetDatabase.LoadAssetAtPath<DialogueTree>(treePath);
            Assert.That(saved.Nodes[0].flagsToSetOnEnter[0], Is.EqualTo(NewId));
            Assert.That(EditorUtility.IsDirty(saved), Is.False);
            Assert.Throws<InvalidOperationException>(() => ContentIdChangeService.Apply(preview));
        }

        [Test]
        public void RenameListsNameBasedMatchesForManualReviewAndPreservesTheirText()
        {
            string treePath = $"{folder}/Dialogue.asset";
            DialogueTree tree = CreateDialogueAsset(treePath);
            var notes = ScriptableObject.CreateInstance<FlagDatabase>();
            using (var data = new SerializedObject(notes))
            {
                SerializedProperty entries = data.FindProperty("flags");
                entries.arraySize = 1;
                SerializedProperty entry = entries.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("id").stringValue = "test.other";
                entry.FindPropertyRelative("description").stringValue = OldId;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            string notesPath = $"{folder}/OtherFlags.asset";
            AssetDatabase.CreateAsset(notes, notesPath);

            ContentIdChangePreview preview = Preview();
            Assert.That(preview.References.Single().AssetPath, Is.EqualTo(treePath));
            Assert.That(preview.ManualReview.Single().AssetPath, Is.EqualTo(notesPath));
            Assert.That(preview.ManualReview[0].PropertyPath, Does.EndWith(".description"));

            ContentIdChangeService.Apply(preview);
            Assert.That(tree.Nodes[0].flagsToSetOnEnter[0], Is.EqualTo(NewId));
            Assert.That(notes.Flags[0].description, Is.EqualTo(OldId));
        }

        [Test]
        public void FlagUsageScansMultipleIdsAndOnlyLoadedScenesWhenRequested()
        {
            var (scene, _) = CreateScene("Open", "another-flag", keepOpen: true);
            CreateScene("Closed", OldId, keepOpen: false);
            string treePath = $"{folder}/Dialogue.asset";
            CreateDialogueAsset(treePath);

            var matches = ContentIdChangeService.FindReferenceMatches(ContentIdKind.Flag,
                new[] { OldId, "another-flag", OldId }, AssetDatabase.GetAssetPath(source), includeClosedScenes: false);

            Assert.That(matches.Select(match => match.AssetPath), Is.EquivalentTo(new[] { treePath, scene.path }));
            Assert.That(matches.All(match => match.CanRename), Is.True);
        }

        [Test]
        public void GameStateRenameUsesMarkedFieldsAcrossStateSystems()
        {
            var database = ScriptableObject.CreateInstance<GameStateDatabase>();
            using (var data = new SerializedObject(database))
            {
                SerializedProperty states = data.FindProperty("states");
                states.arraySize = 1;
                states.GetArrayElementAtIndex(0).FindPropertyRelative("state").stringValue = OldId;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.CreateAsset(database, $"{folder}/GameStates.asset");
            var tension = ScriptableObject.CreateInstance<HorrorTensionDefinition>();
            SetString(tension, "defaultStateId", OldId);
            AssetDatabase.CreateAsset(tension, $"{folder}/Tension.asset");
            var (scene, handler) = CreateScene("Modes", "unrelated", keepOpen: true);
            SceneModeDefinition mode = handler.gameObject.AddComponent<SceneModeDefinition>();
            SetString(mode, "initialGameState", OldId);
            Assert.That(EditorSceneManager.SaveScene(scene), Is.True);

            var preview = ContentIdChangeService.PreviewRename(database, ContentIdKind.GameState, OldId, NewId);
            Assert.That(preview.References.Single().PropertyPath, Is.EqualTo("initialGameState"));
            Assert.That(preview.ManualReview.Single().PropertyPath, Is.EqualTo("defaultStateId"));

            ContentIdChangeService.Apply(preview);
            Assert.That(mode.InitialGameState, Is.EqualTo(NewId));
            Assert.That(tension.DefaultStateId, Is.EqualTo(OldId));
        }

        [Test]
        public void CinematicRenameRecognizesTheConfiguredLauncherId()
        {
            var definition = ScriptableObject.CreateInstance<CinematicDefinition>();
            SetString(definition, "id", OldId);
            using (var data = new SerializedObject(definition))
            {
                SerializedProperty beats = data.FindProperty("beats");
                beats.arraySize = 1;
                beats.GetArrayElementAtIndex(0).FindPropertyRelative("id").stringValue = NewId;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.CreateAsset(definition, $"{folder}/Cinematic.asset");
            var (scene, handler) = CreateScene("Launcher", "unrelated", keepOpen: true);
            var launcher = handler.gameObject.AddComponent<CinematicSceneLauncher>();
            SetString(launcher, "cinematicId", OldId);
            Assert.That(EditorSceneManager.SaveScene(scene), Is.True);

            var preview = ContentIdChangeService.PreviewRename(definition, ContentIdKind.Cinematic, OldId, NewId);
            Assert.That(preview.References.Single().PropertyPath, Is.EqualTo("cinematicId"));
            ContentIdChangeService.Apply(preview);

            using var launcherData = new SerializedObject(launcher);
            Assert.That(launcherData.FindProperty("cinematicId").stringValue, Is.EqualTo(NewId));
        }

        [Test]
        public void ObjectiveRenameSupportsOptedInProjectFields()
        {
            var definition = ScriptableObject.CreateInstance<ObjectiveDefinition>();
            SetString(definition, "id", OldId);
            AssetDatabase.CreateAsset(definition, $"{folder}/Objective.asset");
            var consumer = ScriptableObject.CreateInstance<ContentIdReferenceFixture>();
            consumer.tracked.Add(OldId);
            consumer.objectiveIdNote = OldId;
            AssetDatabase.CreateAsset(consumer, $"{folder}/ObjectiveConsumer.asset");

            var preview = ContentIdChangeService.PreviewRename(definition, ContentIdKind.Objective, OldId, NewId);
            Assert.That(preview.References.Single().PropertyPath, Is.EqualTo("tracked.Array.data[0]"));
            Assert.That(preview.ManualReview.Single().PropertyPath, Is.EqualTo("objectiveIdNote"));
            ContentIdChangeService.Apply(preview);

            Assert.That(definition.Id, Is.EqualTo(NewId));
            Assert.That(consumer.tracked.Single(), Is.EqualTo(NewId));
            Assert.That(consumer.objectiveIdNote, Is.EqualTo(OldId));
        }

        [Test]
        public void SaveFailureRestoresPreviouslyWrittenFilesAndPreservesUnrelatedSceneEdits()
        {
            var (openScene, openHandler) = CreateScene("Open", OldId, keepOpen: true);
            var (prefabScene, prefabHandler) = CreateScene("PrefabSource", "on-disk", keepOpen: true);
            SetFlag(prefabHandler, OldId);
            string prefabPath = $"{folder}/Consumer.prefab";
            PrefabUtility.SaveAsPrefabAsset(prefabHandler.gameObject, prefabPath);
            EditorSceneManager.CloseScene(prefabScene, removeScene: true);
            CreateScene("Closed", OldId, keepOpen: false);
            var (unrelatedScene, unrelatedHandler) = CreateScene("Unrelated", "on-disk", keepOpen: true);
            SetFlag(unrelatedHandler, "unsaved");
            string treePath = $"{folder}/Dialogue.asset";
            CreateDialogueAsset(treePath);
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string[] paths = { openScene.path, $"{folder}/Closed.unity", sourcePath, treePath, prefabPath };
            var originalFiles = paths.ToDictionary(path => path, File.ReadAllBytes);
            var before = CaptureSceneSetup();

            var error = Assert.Throws<InvalidOperationException>(() => ContentIdChangeService.Apply(Preview(), asset =>
            {
                AssetDatabase.SaveAssetIfDirty(asset);
                foreach (string path in paths)
                    Assert.That(File.ReadAllBytes(path), Is.Not.EqualTo(originalFiles[path]), path);
                throw new IOException("Simulated failure after writing the source");
            }));

            Assert.That(error.Message, Does.Contain("original IDs and saved files were restored"));
            Assert.That(error.InnerException, Is.TypeOf<IOException>());
            Assert.That(error.InnerException.Message, Does.Contain("Simulated failure"));
            foreach (string path in paths)
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(originalFiles[path]), path);
            Assert.That(AssetDatabase.LoadAssetAtPath<FlagDatabase>(sourcePath).Flags[0].id, Is.EqualTo(OldId));
            Assert.That(AssetDatabase.LoadAssetAtPath<DialogueTree>(treePath).Nodes[0].flagsToSetOnEnter[0], Is.EqualTo(OldId));
            Assert.That(ReadFlag(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<FlagHandler>()), Is.EqualTo(OldId));
            Assert.That(ReadFlag(openHandler), Is.EqualTo(OldId));
            Assert.That(ReadSavedSceneFlag("Closed"), Is.EqualTo(OldId));
            Assert.That(CaptureSceneSetup(), Is.EqualTo(before));
            Assert.That(unrelatedScene.isDirty, Is.True);
            Assert.That(ReadFlag(unrelatedHandler), Is.EqualTo("unsaved"));
        }

        [Test]
        public void UnsavedAffectedAssetBlocksRenameBeforeAnyWrite()
        {
            string path = $"{folder}/Dialogue.asset";
            DialogueTree tree = CreateDialogueAsset(path);
            tree.name = "Unsaved title";
            EditorUtility.SetDirty(tree);
            byte[] onDisk = File.ReadAllBytes(path);

            var error = Assert.Throws<InvalidOperationException>(() => ContentIdChangeService.Apply(Preview()));

            Assert.That(error.Message, Does.Contain("Save the affected asset"));
            Assert.That(tree.name, Is.EqualTo("Unsaved title"));
            Assert.That(source.Flags[0].id, Is.EqualTo(OldId));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(onDisk));
        }

        [Test]
        public void RenameUpdatesPrefabVariantAndSceneInstanceWhenSourceChangesFirst()
        {
            var (scene, handler) = CreateScene("Z-Instance", OldId, keepOpen: true);
            string basePath = $"{folder}/A-Base.prefab";
            GameObject basePrefab = PrefabUtility.SaveAsPrefabAsset(handler.gameObject, basePath);
            UnityEngine.Object.DestroyImmediate(handler.gameObject);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, scene);
            string variantPath = $"{folder}/B-Variant.prefab";
            GameObject variant = PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            UnityEngine.Object.DestroyImmediate(instance);
            PrefabUtility.InstantiatePrefab(variant, scene);
            Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            EditorSceneManager.CloseScene(scene, removeScene: true);

            ContentIdChangePreview preview = Preview();
            Assert.That(preview.References.Select(edit => edit.AssetPath),
                Is.EquivalentTo(new[] { basePath, variantPath, $"{folder}/Z-Instance.unity" }));
            ContentIdChangeService.Apply(preview);

            Assert.That(ReadFlag(AssetDatabase.LoadAssetAtPath<GameObject>(basePath).GetComponent<FlagHandler>()), Is.EqualTo(NewId));
            Assert.That(ReadFlag(AssetDatabase.LoadAssetAtPath<GameObject>(variantPath).GetComponent<FlagHandler>()), Is.EqualTo(NewId));
            Assert.That(ReadSavedSceneFlag("Z-Instance"), Is.EqualTo(NewId));
        }

        private static (string path, bool loaded, bool active)[] CaptureSceneSetup() =>
            EditorSceneManager.GetSceneManagerSetup().Select(scene => (scene.path, scene.isLoaded, scene.isActive)).ToArray();

        private ContentIdChangePreview Preview() =>
            ContentIdChangeService.PreviewRename(source, ContentIdKind.Flag, OldId, NewId);

        private static DialogueTree CreateDialogueAsset(string path)
        {
            var tree = ScriptableObject.CreateInstance<DialogueTree>();
            using var data = new SerializedObject(tree);
            SerializedProperty nodes = data.FindProperty("nodes");
            nodes.arraySize = 1;
            SerializedProperty flags = nodes.GetArrayElementAtIndex(0).FindPropertyRelative("flagsToSetOnEnter");
            flags.arraySize = 1;
            flags.GetArrayElementAtIndex(0).stringValue = OldId;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(tree, path);
            return tree;
        }

        private (Scene scene, FlagHandler handler) CreateScene(string name, string flag, bool keepOpen)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            FlagHandler handler = CreateHandler(scene, "Consumer");
            SetFlag(handler, flag);
            Assert.That(EditorSceneManager.SaveScene(scene, $"{folder}/{name}.unity"), Is.True);
            if (!keepOpen) EditorSceneManager.CloseScene(scene, removeScene: true);
            return (scene, handler);
        }

        private static FlagHandler CreateHandler(Scene scene, string name)
        {
            Scene previousActiveScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try { return new GameObject(name).AddComponent<FlagHandler>(); }
            finally { SceneManager.SetActiveScene(previousActiveScene); }
        }

        private string ReadSavedSceneFlag(string name)
        {
            Scene scene = EditorSceneManager.OpenPreviewScene($"{folder}/{name}.unity");
            try { return ReadFlag(scene.GetRootGameObjects().Single().GetComponent<FlagHandler>()); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void SetFlag(FlagHandler handler, string flag)
        {
            SetString(handler, "valueFlagId", flag);
            EditorSceneManager.MarkSceneDirty(handler.gameObject.scene);
        }

        private static void SetString(UnityEngine.Object target, string field, string value)
        {
            using var data = new SerializedObject(target);
            data.FindProperty(field).stringValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string ReadFlag(FlagHandler handler)
        {
            using var data = new SerializedObject(handler);
            return data.FindProperty("valueFlagId").stringValue;
        }
    }
}
