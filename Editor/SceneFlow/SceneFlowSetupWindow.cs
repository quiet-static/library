using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.SceneFlow
{
    /// <summary>
    /// General-purpose setup and validation surface for profile-driven scene flow.
    /// </summary>
    public sealed class SceneFlowSetupWindow : EditorWindow
    {
        private SceneBootstrapProfile bootstrapProfile;
        private SceneFlowMap sceneFlowMap;
        private SceneFlowRequestChannel requestChannel;
        private Vector2 scroll;

        [MenuItem(QuietStaticMenuPaths.ProjectSetup, false, 3)]
        public static void Open()
        {
            GetWindow<SceneFlowSetupWindow>("Scene Flow Setup");
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Scene Flow Assets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The bootstrap profile defines startup scene lifetime. The map defines later connections. The channel lets scene-owned objects request transitions without referencing a persistent manager.",
                MessageType.Info);

            bootstrapProfile = (SceneBootstrapProfile)EditorGUILayout.ObjectField(
                "Bootstrap Profile", bootstrapProfile, typeof(SceneBootstrapProfile), false);
            sceneFlowMap = (SceneFlowMap)EditorGUILayout.ObjectField(
                "Scene Flow Map", sceneFlowMap, typeof(SceneFlowMap), false);
            requestChannel = (SceneFlowRequestChannel)EditorGUILayout.ObjectField(
                "Request Channel", requestChannel, typeof(SceneFlowRequestChannel), false);

            DrawAssetCreation();
            EditorGUILayout.Space();
            DrawBuildSettings();
            EditorGUILayout.Space();
            DrawSceneSetup();
            EditorGUILayout.Space();
            DrawOptionalGenerators();
            EditorGUILayout.Space();
            DrawValidation();
            EditorGUILayout.EndScrollView();
        }

        private static void DrawOptionalGenerators()
        {
            EditorGUILayout.LabelField("Optional Project Generators", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "These create maintained starter or sample assets. Review each generator's confirmation before applying it.",
                MessageType.Info);
            if (GUILayout.Button("Generate Cinematic and Readable Examples"))
                QuietStatic.Toolkit.Editor.Cinematics.CinematicExamplePrefabBuilder.Generate();
            if (GUILayout.Button("Build Settings and Pause Prefabs"))
                QuietStatic.Toolkit.Editor.Settings.SettingsMenuPrefabBuilder.BuildAll();
            if (GUILayout.Button("Build Custom Jumpscare Prefab"))
                QuietStatic.Toolkit.Editor.Jumpscare.JumpscarePrefabBuilder.Build();
            if (GUILayout.Button("Build Documentation Sample Scenes"))
                QuietStatic.Toolkit.Editor.Samples.DocumentationSampleSceneBuilder.BuildAll();
        }

        private void DrawAssetCreation()
        {
            EditorGUILayout.LabelField("Create Missing Assets", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = bootstrapProfile == null;
                if (GUILayout.Button("Bootstrap Profile"))
                {
                    bootstrapProfile = CreateAsset<SceneBootstrapProfile>(
                        "SceneBootstrapProfile.asset");
                }
                GUI.enabled = sceneFlowMap == null;
                if (GUILayout.Button("Scene Flow Map"))
                {
                    sceneFlowMap = CreateAsset<SceneFlowMap>("SceneFlowMap.asset");
                }
                GUI.enabled = requestChannel == null;
                if (GUILayout.Button("Request Channel"))
                {
                    requestChannel = CreateAsset<SceneFlowRequestChannel>(
                        "SceneFlowRequestChannel.asset");
                }
                GUI.enabled = true;
            }
        }

        private void DrawBuildSettings()
        {
            EditorGUILayout.LabelField("Build Settings", EditorStyles.boldLabel);
            if (bootstrapProfile == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a bootstrap profile to inspect its scenes.",
                    MessageType.None);
                return;
            }

            List<string> missing = GetMissingBuildSceneNames(bootstrapProfile);
            EditorGUILayout.LabelField(
                missing.Count == 0
                    ? "Every referenced profile scene is enabled in Build Settings."
                    : $"Missing or disabled: {string.Join(", ", missing)}");

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = missing.Count > 0;
                if (GUILayout.Button("Add Located Scenes"))
                {
                    AddLocatedScenesToBuildSettings(bootstrapProfile);
                }
                GUI.enabled = true;
                if (GUILayout.Button("Open Build Settings"))
                {
                    EditorWindow.GetWindow(
                        Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));
                }
            }
        }

        private void DrawSceneSetup()
        {
            EditorGUILayout.LabelField("Current Scene", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                string.IsNullOrWhiteSpace(EditorSceneManager.GetActiveScene().path)
                    ? "Unsaved scene"
                    : EditorSceneManager.GetActiveScene().path);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = bootstrapProfile != null;
                if (GUILayout.Button("Create Bootstrap Object"))
                {
                    CreateBootstrapObject();
                }
                GUI.enabled = true;
                if (GUILayout.Button("Create Scene Flow Manager"))
                {
                    CreateManagerObject();
                }
            }
        }

        private void DrawValidation()
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            if (bootstrapProfile == null)
            {
                EditorGUILayout.HelpBox("No bootstrap profile assigned.", MessageType.Warning);
                return;
            }

            if (!bootstrapProfile.IsValid)
            {
                EditorGUILayout.HelpBox(
                    "The bootstrap profile needs an initial content scene.",
                    MessageType.Error);
            }

            if (bootstrapProfile.PersistentSceneNames.Contains(
                    bootstrapProfile.InitialSceneName))
            {
                EditorGUILayout.HelpBox(
                    "The initial content scene is also persistent. It will not be replaced by later normal transitions.",
                    MessageType.Warning);
            }

            if (FindAnyObjectByType<SceneBootstrapper>() == null)
            {
                EditorGUILayout.HelpBox(
                    "The current scene has no SceneBootstrapper.",
                    MessageType.Warning);
            }

            SceneFlowManager manager = FindAnyObjectByType<SceneFlowManager>();
            if (manager != null)
            {
                EditorGUILayout.HelpBox(
                    "A SceneFlowManager exists in the current scene. Normally it belongs in one of the profile's persistent scenes, not the tiny bootstrap scene.",
                    MessageType.Info);
            }
        }

        private void CreateBootstrapObject()
        {
            GameObject gameObject = new("Scene Bootstrapper");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Scene Bootstrapper");
            SceneBootstrapper bootstrapper = gameObject.AddComponent<SceneBootstrapper>();
            SerializedObject serialized = new(bootstrapper);
            serialized.FindProperty("profile").objectReferenceValue = bootstrapProfile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Selection.activeGameObject = gameObject;
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        private void CreateManagerObject()
        {
            GameObject gameObject = new("Scene Flow Manager");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Scene Flow Manager");
            SceneFlowManager manager = gameObject.AddComponent<SceneFlowManager>();
            SerializedObject serialized = new(manager);
            serialized.FindProperty("loadStartupSceneOnAwake").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (requestChannel != null)
            {
                manager.SetRequestChannel(requestChannel);
            }
            Selection.activeGameObject = gameObject;
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        private static T CreateAsset<T>(string defaultName)
            where T : ScriptableObject
        {
            string path = EditorUtility.SaveFilePanelInProject(
                $"Create {typeof(T).Name}",
                Path.GetFileNameWithoutExtension(defaultName),
                "asset",
                "Choose where to save the scene-flow asset.");
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            T asset = CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            return asset;
        }

        private static List<string> GetMissingBuildSceneNames(
            SceneBootstrapProfile profile)
        {
            HashSet<string> enabledNames = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => Path.GetFileNameWithoutExtension(scene.path))
                .ToHashSet(StringComparer.Ordinal);
            return profile.ReferencedSceneNames
                .Where(name => !enabledNames.Contains(name))
                .ToList();
        }

        private static void AddLocatedScenesToBuildSettings(
            SceneBootstrapProfile profile)
        {
            List<EditorBuildSettingsScene> scenes =
                EditorBuildSettings.scenes.ToList();
            HashSet<string> existingPaths = scenes
                .Select(scene => scene.path)
                .ToHashSet(StringComparer.Ordinal);

            foreach (string sceneName in GetMissingBuildSceneNames(profile))
            {
                string[] matches = AssetDatabase.FindAssets($"{sceneName} t:Scene")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        sceneName,
                        StringComparison.Ordinal))
                    .ToArray();

                if (matches.Length != 1)
                {
                    GameLogger.Warning(
                        nameof(SceneFlowSetupWindow),
                        null,
                        $"Could not add scene '{sceneName}': expected one exact project match, found {matches.Length}."
                     );
                    continue;
                }

                if (existingPaths.Add(matches[0]))
                {
                    scenes.Add(new EditorBuildSettingsScene(matches[0], true));
                }
                else
                {
                    EditorBuildSettingsScene existing = scenes.First(
                        scene => scene.path == matches[0]);
                    existing.enabled = true;
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
