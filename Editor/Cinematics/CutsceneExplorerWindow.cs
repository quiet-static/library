using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Cinematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.Cinematics
{
    /// <summary>Browses, validates, creates, and previews cutscene runners.</summary>
    public sealed class CutsceneExplorerWindow : EditorWindow
    {
        private readonly List<CutsceneSequenceRunner> runners = new();
        private readonly List<DialogueNodeCinematicCue> dialogueCues = new();
        private readonly List<Scene> scenes = new();
        private Vector2 runnerScroll;
        private Vector2 editorScroll;
        private string search = string.Empty;
        private int sceneHandle;
        private bool selectionScope;
        private bool sequenceSettingsExpanded;
        private int view;
        private CutsceneSequenceRunner selectedRunner;
        private DialogueNodeCinematicCue selectedCue;
        private readonly Dictionary<int, int> selectedSteps = new();

        public static void Open()
        {
            var window = GetWindow<CutsceneExplorerWindow>("Cutscenes");
            window.minSize = new Vector2(580f, 310f);
        }

        internal static void Open(CutsceneSequenceRunner runner)
        {
            Open();
            var window = GetWindow<CutsceneExplorerWindow>();
            window.Refresh();
            window.view = 0;
            window.selectionScope = false;
            window.sceneHandle = runner.gameObject.scene.handle;
            window.search = string.Empty;
            window.SelectRunner(runner);
        }

        private void OnEnable()
        {
            Refresh();
            EditorApplication.hierarchyChanged += Refresh;
            Selection.selectionChanged += Repaint;
            PrefabStage.prefabStageOpened += RefreshForPrefabStage;
            PrefabStage.prefabStageClosing += RefreshAfterPrefabStage;
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= Refresh;
            Selection.selectionChanged -= Repaint;
            PrefabStage.prefabStageOpened -= RefreshForPrefabStage;
            PrefabStage.prefabStageClosing -= RefreshAfterPrefabStage;
            Undo.undoRedoPerformed -= Repaint;
            EditorApplication.delayCall -= Refresh;
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying) Repaint();
        }

        private void OnGUI()
        {
            bool searchChanged;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string updatedSearch = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"));
                searchChanged = updatedSearch != search;
                search = updatedSearch;
                DrawScope();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("Create", EditorStyles.toolbarButton, GUILayout.Width(55f))) CreateCutscene();
                if (GUILayout.Button("Launch Routes", EditorStyles.toolbarButton, GUILayout.Width(86f)))
                    CinematicLaunchOverview.Open();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(65f)))
                    Refresh();
            }

            view = GUILayout.Toolbar(view, new[] { "Sequences", "Dialogue Cues" });
            bool prefabMode = PrefabStageUtility.GetCurrentPrefabStage() != null;
            bool useSelection = !prefabMode && selectionScope;
            Transform[] selection = useSelection ? Selection.transforms : Array.Empty<Transform>();
            bool InScope(Component item) => item != null &&
                (prefabMode || sceneHandle == 0 || item.gameObject.scene.handle == sceneHandle) &&
                (!useSelection || selection.Any(root => root != null &&
                    (item.transform == root || item.transform.IsChildOf(root))));
            if (view == 1)
            {
                DrawDialogueCues(dialogueCues.Where(cue => InScope(cue) && MatchesCue(cue, search)).ToArray());
                return;
            }

            CutsceneSequenceRunner[] visible = runners.Where(runner => InScope(runner) && MatchesSearch(runner, search)).ToArray();
            if (visible.Length == 0)
            {
                EditorGUILayout.HelpBox("No cutscenes match the current scope and search.", MessageType.Info);
                return;
            }
            if (selectedRunner == null || !visible.Contains(selectedRunner)) SelectRunner(visible[0]);
            if (searchChanged) SelectFirstMatchingStep();

            using (new EditorGUILayout.HorizontalScope())
            {
                float paneWidth = Mathf.Clamp(position.width * .28f, 150f, 250f);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(paneWidth)))
                {
                    EditorGUILayout.LabelField($"Cutscenes ({visible.Length})", EditorStyles.boldLabel);
                    runnerScroll = EditorGUILayout.BeginScrollView(runnerScroll);
                    foreach (CutsceneSequenceRunner runner in visible)
                    {
                        var label = new GUIContent(runner.DisplayName, $"{runner.gameObject.scene.name} / {HierarchyPath(runner.transform)}");
                        if (GUILayout.Toggle(runner == selectedRunner, label, EditorStyles.miniButton) && runner != selectedRunner)
                            SelectRunner(runner);
                        EditorGUILayout.LabelField(runner.gameObject.scene.name, EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndScrollView();
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    editorScroll = EditorGUILayout.BeginScrollView(editorScroll);
                    DrawRunner(selectedRunner);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawScope()
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                GUILayout.Label("Prefab Mode", EditorStyles.miniLabel, GUILayout.Width(80f));
                return;
            }

            string[] options = new[] { "All loaded scenes", "Hierarchy selection" }
                .Concat(scenes.Select(scene => scene.name)).ToArray();
            int current = selectionScope ? 1 : sceneHandle == 0 ? 0 : scenes.FindIndex(scene => scene.handle == sceneHandle) + 2;
            int picked = EditorGUILayout.Popup(current, options, EditorStyles.toolbarPopup, GUILayout.Width(140f));
            selectionScope = picked == 1;
            sceneHandle = picked >= 2 ? scenes[picked - 2].handle : 0;
        }

        private void DrawDialogueCues(DialogueNodeCinematicCue[] visible)
        {
            if (visible.Length == 0)
            {
                EditorGUILayout.HelpBox("No dialogue cues match the current scope and search.", MessageType.Info);
                return;
            }

            string[] labels = visible.Select(cue => $"{cue.gameObject.scene.name} / {HierarchyPath(cue.transform)}").ToArray();
            int index = Mathf.Max(0, Array.IndexOf(visible, selectedCue));
            selectedCue = visible[EditorGUILayout.Popup("Dialogue Cues", index, labels)];
            if (GUILayout.Button("Select Cue Object")) Selection.activeGameObject = selectedCue.gameObject;

            using var scroll = new EditorGUILayout.ScrollViewScope(editorScroll);
            editorScroll = scroll.scrollPosition;
            using var data = new SerializedObject(selectedCue);
            SerializedProperty player = data.FindProperty("dialoguePlayer");
            SerializedProperty runner = data.FindProperty("dialogueRunner");
            SerializedProperty cues = data.FindProperty("cues");
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.PropertyField(player);
                EditorGUILayout.PropertyField(runner);
                if (player.objectReferenceValue == null && runner.objectReferenceValue == null)
                    EditorGUILayout.HelpBox("Assign the dialogue player or runner whose nodes activate these cues.", MessageType.Warning);
                EditorGUILayout.PropertyField(cues, true);
                data.ApplyModifiedProperties();
            }
        }

        private static bool MatchesCue(DialogueNodeCinematicCue cue, string search)
        {
            string query = search.Trim();
            if (query.Length == 0 || Contains(cue.gameObject.scene.name, query) || Contains(HierarchyPath(cue.transform), query))
                return true;

            using var data = new SerializedObject(cue);
            SerializedProperty cues = data.FindProperty("cues");
            for (int index = 0; index < cues.arraySize; index++)
            {
                SerializedProperty item = cues.GetArrayElementAtIndex(index);
                if (Contains(item.FindPropertyRelative("nodeId").stringValue, query) ||
                    Contains(item.FindPropertyRelative("cameraShotId").stringValue, query)) return true;
            }
            return false;
        }

        private void SelectRunner(CutsceneSequenceRunner runner)
        {
            selectedRunner = runner;
            editorScroll = Vector2.zero;
            SelectFirstMatchingStep();
        }

        private void SelectFirstMatchingStep()
        {
            if (selectedRunner == null || string.IsNullOrWhiteSpace(search)) return;
            for (int index = 0; index < selectedRunner.Steps.Count; index++)
                if (MatchesStep(selectedRunner.Steps[index], search.Trim()))
                {
                    selectedSteps[selectedRunner.GetInstanceID()] = index;
                    return;
                }
        }

        private void DrawRunner(CutsceneSequenceRunner runner)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(runner.DisplayName, EditorStyles.boldLabel);
                if (GUILayout.Button("Select Object", GUILayout.Width(85f)))
                {
                    Selection.activeObject = runner.gameObject;
                    EditorGUIUtility.PingObject(runner.gameObject);
                }
            }
            EditorGUILayout.LabelField($"{runner.gameObject.scene.name} / {HierarchyPath(runner.transform)}", EditorStyles.wordWrappedMiniLabel);
            DrawEditor(runner);
        }

        private void DrawEditor(CutsceneSequenceRunner runner)
        {
            using var data = new SerializedObject(runner);
            data.Update();
            SerializedProperty steps = data.FindProperty("steps");
            int key = runner.GetInstanceID();
            selectedSteps.TryGetValue(key, out int selected);
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, steps.arraySize - 1));
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Add Step"))
                {
                    AddStep(steps);
                    selected = steps.arraySize - 1;
                }
            }
            if (steps.arraySize > 0)
            {
                string[] labels = Enumerable.Range(0, steps.arraySize).Select(index =>
                {
                    string name = steps.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue;
                    return $"{index + 1}. {(string.IsNullOrWhiteSpace(name) ? "Step " + (index + 1) : name)}";
                }).ToArray();
                selected = EditorGUILayout.Popup("Step", selected, labels);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(selected == 0))
                        if (GUILayout.Button("Move Up")) { steps.MoveArrayElement(selected, selected - 1); selected--; }
                    using (new EditorGUI.DisabledScope(selected == steps.arraySize - 1))
                        if (GUILayout.Button("Move Down")) { steps.MoveArrayElement(selected, selected + 1); selected++; }
                    if (GUILayout.Button("Duplicate")) { steps.InsertArrayElementAtIndex(selected); selected++; }
                    if (GUILayout.Button("Delete")) { steps.DeleteArrayElementAtIndex(selected); selected = Mathf.Max(0, selected - 1); }
                }
            }
            selectedSteps[key] = selected;
            data.ApplyModifiedProperties();
            DrawPlayback(runner, selected);

            data.Update();
            if (steps.arraySize == 0)
                EditorGUILayout.HelpBox("Add a step to give this cutscene an activity or timing.", MessageType.Warning);
            else
            {
                foreach (string problem in GetStepProblems(runner.Steps[selected]))
                    EditorGUILayout.HelpBox(problem, MessageType.Warning);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    DrawStep(steps.GetArrayElementAtIndex(selected));
            }

            sequenceSettingsExpanded = EditorGUILayout.Foldout(sequenceSettingsExpanded, "Sequence Settings", true);
            if (sequenceSettingsExpanded)
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    foreach (string field in new[] { "playOnStart", "useUnscaledTime", "fadeChannel", "fader",
                                 "fadeOutBeforeStep", "fadeInAfterCameraMove", "onSequenceStarted", "onSequenceFinished" })
                        EditorGUILayout.PropertyField(data.FindProperty(field));
            }
            data.ApplyModifiedProperties();
        }

        private static void DrawPlayback(CutsceneSequenceRunner runner, int selected)
        {
            bool canPlay = EditorApplication.isPlaying && runner.isActiveAndEnabled && !runner.IsRunning;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!canPlay || runner.Steps.Count == 0))
                    if (GUILayout.Button("Play All")) runner.Play();
                using (new EditorGUI.DisabledScope(!canPlay || selected >= runner.Steps.Count || runner.Steps[selected] == null))
                    if (GUILayout.Button("Play Step")) runner.PlayStep(selected);
                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || !runner.IsRunning))
                    if (GUILayout.Button("Stop")) runner.Stop();
            }
            if (!EditorApplication.isPlaying)
                EditorGUILayout.LabelField("Playback controls are available in Play Mode.", EditorStyles.miniLabel);
            else if (runner.IsRunning && runner.CurrentStepIndex >= 0 && runner.CurrentStepIndex < runner.Steps.Count)
                EditorGUILayout.LabelField($"Playing step {runner.CurrentStepIndex + 1} · {runner.Steps[runner.CurrentStepIndex]?.name}",
                    EditorStyles.wordWrappedMiniLabel);
        }

        private static void DrawStep(SerializedProperty step)
        {
            EditorGUILayout.PropertyField(step.FindPropertyRelative("name"));
            EditorGUILayout.PropertyField(step.FindPropertyRelative("cameraDirector"));
            EditorGUILayout.PropertyField(step.FindPropertyRelative("cameraShotId"));
            if (GUILayout.Button("Find Shot in Scenes / Project Prefabs"))
            {
                step.serializedObject.ApplyModifiedProperties();
                CutsceneContentSearch.ShowShots(GUILayoutUtility.GetLastRect(), step);
            }
            var director = step.FindPropertyRelative("cameraDirector").objectReferenceValue as CinematicCutsceneCameraDirector;
            using (new EditorGUI.DisabledScope(director == null))
                if (GUILayout.Button("Edit Camera Shots")) CinematicShotEditorWindow.Open(director);
            var controllerProperty = step.FindPropertyRelative("characterController");
            EditorGUILayout.PropertyField(controllerProperty);
            var controller = controllerProperty.objectReferenceValue as CutsceneCharacterController;
            if (controller != null && GUILayout.Button("Select Character Setup")) Selection.activeObject = controller;
            var actions = step.FindPropertyRelative("characterActions");
            for (int i = 0; i < actions.arraySize; i++)
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Character Action {i + 1}", EditorStyles.boldLabel);
                DrawAction(actions.GetArrayElementAtIndex(i), controller);
                if (GUILayout.Button("Remove Character Action")) { actions.DeleteArrayElementAtIndex(i); break; }
            }
            if (GUILayout.Button("Add Character Action")) AddCharacterAction(actions);
            EditorGUILayout.PropertyField(step.FindPropertyRelative("dialogueRunner"));
            EditorGUILayout.PropertyField(step.FindPropertyRelative("waitSource"));
            EditorGUILayout.PropertyField(step.FindPropertyRelative("waitBeforeActivity"));
            EditorGUILayout.PropertyField(step.FindPropertyRelative("waitAfterStep"));
            EditorGUILayout.HelpBox("Animations start with the step. Use timing or a wait source to keep the shot on screen while they play.", MessageType.Info);
            EditorGUILayout.PropertyField(step.FindPropertyRelative("onStepStarted"));
            EditorGUILayout.PropertyField(step.FindPropertyRelative("onStepFinished"));
        }

        private static void DrawAction(SerializedProperty action, CutsceneCharacterController controller)
        {
            EditorGUILayout.PropertyField(action.FindPropertyRelative("characterId"));
            if (controller != null && GUILayout.Button("Find Character"))
            {
                action.serializedObject.ApplyModifiedProperties();
                CutsceneContentSearch.ShowCharacters(GUILayoutUtility.GetLastRect(), action, controller);
            }
            foreach (string field in new[] { "spawnIfMissing", "spawnPoint", "setPosition", "setRotation", "setActive", "activeState", "faceTarget", "targetToFace", "yawOnly", "smoothFaceTarget", "faceDuration", "animatorOverride" })
                EditorGUILayout.PropertyField(action.FindPropertyRelative(field));
            EditorGUILayout.PropertyField(action.FindPropertyRelative("animatorStateName"));
            EditorGUILayout.PropertyField(action.FindPropertyRelative("animatorLayer"));
            EditorGUILayout.PropertyField(action.FindPropertyRelative("normalizedStartTime"));
            EditorGUILayout.PropertyField(action.FindPropertyRelative("animatorTrigger"));
            if (GUILayout.Button("Find Animation / Trigger in Project"))
            {
                action.serializedObject.ApplyModifiedProperties();
                CutsceneContentSearch.ShowAnimations(GUILayoutUtility.GetLastRect(), action, controller);
            }
        }

        /// <summary>Adds a clean step instead of inheriting the preceding step's events and references.</summary>
        private static void AddStep(SerializedProperty steps)
        {
            int index = steps.arraySize++;
            var step = steps.GetArrayElementAtIndex(index);
            step.FindPropertyRelative("name").stringValue = $"Step {index + 1}";
            foreach (string field in new[] { "cameraDirector", "characterController", "dialogueRunner", "waitSource" })
                step.FindPropertyRelative(field).objectReferenceValue = null;
            step.FindPropertyRelative("cameraShotId").stringValue = string.Empty;
            step.FindPropertyRelative("characterActions").arraySize = 0;
            step.FindPropertyRelative("waitBeforeActivity").floatValue = 0f;
            step.FindPropertyRelative("waitAfterStep").floatValue = 0f;
            foreach (string field in new[] { "onStepStarted", "onStepFinished" })
                step.FindPropertyRelative(field).FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize = 0;
        }

        private static void AddCharacterAction(SerializedProperty actions)
        {
            var action = actions.GetArrayElementAtIndex(actions.arraySize++);
            foreach (string field in new[] { "characterId", "animatorTrigger", "animatorStateName" }) action.FindPropertyRelative(field).stringValue = string.Empty;
            foreach (string field in new[] { "spawnPoint", "targetToFace", "animatorOverride" }) action.FindPropertyRelative(field).objectReferenceValue = null;
            foreach (string field in new[] { "spawnIfMissing", "setPosition", "setRotation", "setActive", "activeState", "yawOnly", "smoothFaceTarget" }) action.FindPropertyRelative(field).boolValue = true;
            action.FindPropertyRelative("faceTarget").boolValue = false;
            action.FindPropertyRelative("faceDuration").floatValue = 0.35f;
            action.FindPropertyRelative("animatorLayer").intValue = 0;
            action.FindPropertyRelative("normalizedStartTime").floatValue = 0f;
        }

        internal static bool MatchesSearch(CutsceneSequenceRunner runner, string search)
        {
            if (runner == null) return false;
            if (string.IsNullOrWhiteSpace(search)) return true;
            string query = search.Trim();
            Scene scene = runner.gameObject.scene;
            return Contains(scene.name, query) || Contains(scene.path, query) || Contains(HierarchyPath(runner.transform), query) ||
                   runner.Steps.Any(step => MatchesStep(step, query));
        }

        private static bool MatchesStep(CutsceneSequenceRunner.Step step, string query)
        {
            if (step == null) return false;
            if (Contains(step.name, query) || Contains(step.cameraShotId, query)) return true;
            if (step.cameraDirector != null && (Contains(step.cameraDirector.name, query) ||
                (step.cameraDirector.TryGetShotIndex(step.cameraShotId, out int index) &&
                    Contains(step.cameraDirector.GetShotDisplayName(index), query)))) return true;

            return step.characterActions?.Any(action => action != null &&
                (Contains(action.characterId, query) || Contains(action.animatorStateName, query) || Contains(action.animatorTrigger, query))) == true;
        }

        private static bool Contains(string value, string query) =>
            value != null && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string HierarchyPath(Transform target) => target.parent == null ? target.name :
            HierarchyPath(target.parent) + "/" + target.name;

        internal static IEnumerable<string> GetStepProblems(CutsceneSequenceRunner.Step step)
        {
            if (step == null)
            {
                yield return "This step is missing. Add a step or remove the empty entry.";
                yield break;
            }

            bool hasDirector = step.cameraDirector != null;
            bool hasShot = !string.IsNullOrWhiteSpace(step.cameraShotId);
            if (hasDirector != hasShot)
                yield return "Assign both Camera Director and Camera Shot to change shots on this step.";
            else if (hasDirector && !step.cameraDirector.TryGetShotIndex(step.cameraShotId, out _))
                yield return $"Shot '{step.cameraShotId}' is missing on the assigned director. Choose a Camera Shot.";

            if (step.waitSource != null && step.waitSource is not ICinematicWaitSource)
                yield return "Choose a Wait Source component that implements ICinematicWaitSource.";

            if (step.characterActions == null || step.characterActions.Count == 0) yield break;
            if (step.characterController == null)
            {
                yield return "Assign a Character Controller to run this step's character actions.";
                yield break;
            }

            for (int i = 0; i < step.characterActions.Count; i++)
            {
                var action = step.characterActions[i];
                string label = $"Character Action {i + 1}";
                if (action == null || string.IsNullOrWhiteSpace(action.characterId))
                {
                    yield return $"{label} needs a Character ID. Use Find Character to choose one.";
                    continue;
                }

                int bindingCount = CutsceneContentSearch.FindCharacterBindings(step.characterController, action.characterId,
                    out GameObject instance, out GameObject prefab);
                if (bindingCount != 1)
                {
                    yield return bindingCount == 0
                        ? $"{label} uses unknown character '{action.characterId}'. Choose a Character ID defined on the controller."
                        : $"{label} uses duplicate Character ID '{action.characterId}'. Give the controller's entries unique IDs.";
                    continue;
                }
                if (instance == null && prefab == null)
                {
                    yield return $"Assign an Existing Instance or Character Prefab for '{action.characterId}' on the controller.";
                    continue;
                }

                bool usesState = !string.IsNullOrWhiteSpace(action.animatorStateName);
                bool usesTrigger = !string.IsNullOrWhiteSpace(action.animatorTrigger);
                if (!usesState && !usesTrigger) continue;

                GameObject character = instance != null ? instance : prefab;
                Animator animator = action.animatorOverride != null ? action.animatorOverride : character.GetComponentInChildren<Animator>(true);
                RuntimeAnimatorController runtime = animator != null ? animator.runtimeAnimatorController : null;
                if (runtime == null)
                {
                    yield return $"{label} needs an Animator with a controller to play its configured animation.";
                    continue;
                }
                if (usesState && !CutsceneContentSearch.FindStates(runtime).Any(state =>
                        (action.animatorLayer == -1 || state.Layer == action.animatorLayer) &&
                        (state.Path == action.animatorStateName || state.Name == action.animatorStateName)))
                    yield return $"{label} cannot play state '{action.animatorStateName}' on layer {action.animatorLayer}. Use Find Animation / Trigger in Project.";
                if (usesTrigger && !CutsceneContentSearch.FindTriggers(runtime).Contains(action.animatorTrigger))
                    yield return $"{label} cannot find trigger '{action.animatorTrigger}'. Use Find Animation / Trigger in Project.";
            }
        }

        private void Refresh() => Refresh(PrefabStageUtility.GetCurrentPrefabStage());

        private void RefreshForPrefabStage(PrefabStage stage) => Refresh(stage);

        private void RefreshAfterPrefabStage(PrefabStage stage) => EditorApplication.delayCall += Refresh;

        private void Refresh(PrefabStage stage)
        {
            runners.Clear();
            dialogueCues.Clear();
            scenes.Clear();
            if (stage != null)
            {
                if (stage.prefabContentsRoot != null)
                    AddSceneContents(stage.prefabContentsRoot);
            }
            else
            {
                for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                {
                    Scene scene = SceneManager.GetSceneAt(sceneIndex);
                    if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;
                    scenes.Add(scene);
                    foreach (GameObject root in scene.GetRootGameObjects())
                        AddSceneContents(root);
                }
                if (scenes.All(scene => scene.handle != sceneHandle)) sceneHandle = 0;
            }
            runners.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
            Repaint();
        }

        private void AddSceneContents(GameObject root)
        {
            runners.AddRange(root.GetComponentsInChildren<CutsceneSequenceRunner>(true));
            dialogueCues.AddRange(root.GetComponentsInChildren<DialogueNodeCinematicCue>(true));
        }

        private void CreateCutscene()
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            GameObject scope = stage != null ? stage.prefabContentsRoot : selectionScope ? Selection.activeGameObject : null;
            if (scope != null && !scope.scene.IsValid()) scope = null;
            Scene targetScene = scope != null ? scope.scene : scenes.Find(scene => scene.handle == sceneHandle);
            GameObject root = new("Cutscene");
            if (targetScene.IsValid() && targetScene != root.scene) SceneManager.MoveGameObjectToScene(root, targetScene);
            if (scope != null) root.transform.SetParent(scope.transform, false);
            Undo.RegisterCreatedObjectUndo(root, "Create Cutscene");
            CutsceneSequenceRunner runner = Undo.AddComponent<CutsceneSequenceRunner>(root);
            using var serializedRunner = new SerializedObject(runner);
            SerializedProperty steps = serializedRunner.FindProperty("steps");
            steps.arraySize = 1;
            steps.GetArrayElementAtIndex(0).FindPropertyRelative("name").stringValue = "Opening Shot";
            serializedRunner.ApplyModifiedProperties();
            Selection.activeGameObject = root;
            SelectRunner(runner);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Refresh();
        }
    }
    /// <summary>On-demand project searches that assign only playable character states and scene-safe shots.</summary>
    internal static class CutsceneContentSearch
    {
        private sealed class Item : AdvancedDropdownItem
        {
            internal Item(string label, Action pick, bool available = true) : base(label) { Pick = pick; enabled = available; }
            internal Action Pick { get; }
        }

        private sealed class Search : AdvancedDropdown
        {
            private readonly string title;
            private readonly List<Item> items;
            internal Search(string title, List<Item> items) : base(new AdvancedDropdownState())
            { this.title = title; this.items = items; minimumSize = new Vector2(640f, 360f); }
            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem(title);
                foreach (var item in items) root.AddChild(item);
                return root;
            }
            protected override void ItemSelected(AdvancedDropdownItem item) { if (item is Item picked) picked.Pick?.Invoke(); }
        }

        private static Action<Action<SerializedProperty>> Capture(SerializedProperty property)
        {
            UnityEngine.Object target = property.serializedObject.targetObject;
            string path = property.propertyPath;
            string version = EditorJsonUtility.ToJson(target);
            return change =>
            {
                if (target == null) return;
                if (EditorJsonUtility.ToJson(target) != version)
                {
                    EditorUtility.DisplayDialog("Cutscene Changed", "The cutscene changed while searching. Reopen the search to select this item.", "OK");
                    return;
                }
                using var data = new SerializedObject(target);
                data.Update();
                var current = data.FindProperty(path);
                if (current == null) return;
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Select Cutscene Content");
                change(current);
                data.ApplyModifiedProperties();
                Undo.CollapseUndoOperations(group);
            };
        }

        internal static void ShowCharacters(Rect rect, SerializedProperty action, CutsceneCharacterController controller)
        {
            var edit = Capture(action);
            var items = new List<Item> { new("None", () => edit(p => p.FindPropertyRelative("characterId").stringValue = string.Empty)) };
            using var data = new SerializedObject(controller);
            var characters = data.FindProperty("characters");
            for (int i = 0; i < characters.arraySize; i++)
            {
                string id = characters.GetArrayElementAtIndex(i).FindPropertyRelative("characterId").stringValue;
                if (string.IsNullOrWhiteSpace(id)) continue;
                items.Add(new Item(id, () => edit(p =>
                {
                    p.FindPropertyRelative("characterId").stringValue = id;
                    p.FindPropertyRelative("animatorOverride").objectReferenceValue = null;
                    p.FindPropertyRelative("animatorStateName").stringValue = string.Empty;
                    p.FindPropertyRelative("animatorTrigger").stringValue = string.Empty;
                    p.FindPropertyRelative("animatorLayer").intValue = 0;
                })));
            }
            new Search("Character IDs from the assigned controller", items).Show(rect);
        }

        internal static void ShowShots(Rect rect, SerializedProperty step)
        {
            var edit = Capture(step);
            var owner = step.serializedObject.targetObject as Component;
            var destination = step.FindPropertyRelative("cameraDirector").objectReferenceValue as CinematicCutsceneCameraDirector;
            var items = new List<Item> { new("None", () => edit(p => p.FindPropertyRelative("cameraShotId").stringValue = string.Empty)) };
            foreach (var director in Resources.FindObjectsOfTypeAll<CinematicCutsceneCameraDirector>()
                         .Where(d => d.gameObject.scene.IsValid() && d.gameObject.scene.isLoaded)
                         .OrderBy(d => d.name))
            {
                for (int i = 0; i < director.ShotCount; i++)
                {
                    string id = director.GetExplicitShotId(i);
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    items.Add(new Item($"Scene: {director.gameObject.scene.name} / {director.name} / {director.GetShotDisplayName(i)} [{id}]", () => edit(p =>
                    {
                        if (director == null) return;
                        p.FindPropertyRelative("cameraDirector").objectReferenceValue = director;
                        p.FindPropertyRelative("cameraShotId").stringValue = id;
                    }), owner != null && director.gameObject.scene == owner.gameObject.scene && director.IsShotUsable(i)));
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                foreach (var source in prefab.GetComponentsInChildren<CinematicCutsceneCameraDirector>(true))
                for (int i = 0; i < source.ShotCount; i++)
                {
                    int shotIndex = i;
                    items.Add(new Item($"Copy prefab framing: {path} / {source.name} / {source.GetShotDisplayName(i)}", () => edit(p =>
                    {
                        if (source == null || destination == null) return;
                        string propertyPath = p.propertyPath;
                        var data = p.serializedObject;
                        int added = CopyShot(source, shotIndex, destination);
                        data.Update();
                        p = data.FindProperty(propertyPath);
                        p.FindPropertyRelative("cameraDirector").objectReferenceValue = destination;
                        p.FindPropertyRelative("cameraShotId").stringValue = destination.GetExplicitShotId(added);
                    }), destination != null && owner != null && destination.gameObject.scene == owner.gameObject.scene && source.IsShotUsable(i)));
                }
            }
            items.Add(new Item("Prefab shots copy framing into the assigned scene director; assign one to enable copying.", null, false));
            new Search("Available Camera Shots", items).Show(rect);
        }

        private static int CopyShot(CinematicCutsceneCameraDirector source, int index, CinematicCutsceneCameraDirector destination)
        {
            using var data = new SerializedObject(source);
            var shot = data.FindProperty("shots").GetArrayElementAtIndex(index);
            var marker = shot.FindPropertyRelative("cameraPositionMarker").objectReferenceValue as Transform;
            var focus = shot.FindPropertyRelative("focusTarget").objectReferenceValue as Transform;
            Vector3 position = marker != null ? marker.position : focus.TransformPoint(shot.FindPropertyRelative("targetRelativeOffset").vector3Value);
            Vector3 direction = focus != null ? focus.position + shot.FindPropertyRelative("lookAtOffset").vector3Value - position : Vector3.zero;
            Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : marker != null ? marker.rotation : source.transform.rotation;
            float fov = shot.FindPropertyRelative("changeFieldOfView").boolValue ? shot.FindPropertyRelative("fieldOfView").floatValue : source.CutsceneCamera != null ? source.CutsceneCamera.fieldOfView : 45f;
            return CinematicShotAuthoring.Create(destination, source.GetShotDisplayName(index) + " Copy", position, rotation, fov);
        }

        internal sealed class AnimationState
        {
            internal string Path;
            internal string Name;
            internal int Layer;
            internal AnimationClip[] Clips;
        }

        internal static List<AnimationState> FindStates(RuntimeAnimatorController runtime)
        {
            var overrides = runtime as AnimatorOverrideController;
            var result = new List<AnimationState>();
            if (GetBaseController(runtime) is not AnimatorController controller) return result;
            for (int layer = 0; layer < controller.layers.Length; layer++)
            {
                var settings = controller.layers[layer];
                int sourceLayer = settings.syncedLayerIndex >= 0 ? settings.syncedLayerIndex : layer;
                Visit(controller.layers[sourceLayer].stateMachine, settings.name, layer, result, overrides);
            }
            return result;
        }

        private static void Visit(AnimatorStateMachine machine, string prefix, int layer, List<AnimationState> result, AnimatorOverrideController overrides)
        {
            foreach (var entry in machine.states)
            {
                var clips = new List<AnimationClip>();
                CollectClips(entry.state.motion, clips);
                result.Add(new AnimationState { Path = prefix + "." + entry.state.name, Name = entry.state.name, Layer = layer,
                    Clips = clips.Select(clip => overrides != null && overrides[clip] != null ? overrides[clip] : clip).Distinct().ToArray() });
            }
            foreach (var child in machine.stateMachines) Visit(child.stateMachine, prefix + "." + child.stateMachine.name, layer, result, overrides);
        }

        private static void CollectClips(Motion motion, List<AnimationClip> clips)
        {
            if (motion is AnimationClip clip) clips.Add(clip);
            else if (motion is BlendTree blend)
                foreach (var child in blend.children) CollectClips(child.motion, clips);
        }

        private static Animator ResolveAnimator(SerializedProperty action, CutsceneCharacterController controller)
        {
            var animator = action.FindPropertyRelative("animatorOverride").objectReferenceValue as Animator;
            if (animator != null || controller == null) return animator;
            string id = action.FindPropertyRelative("characterId").stringValue;
            FindCharacterBindings(controller, id, out GameObject instance, out GameObject prefab);
            GameObject character = instance != null ? instance : prefab;
            return character != null ? character.GetComponentInChildren<Animator>(true) : null;
        }

        internal static int FindCharacterBindings(CutsceneCharacterController controller, string id,
            out GameObject instance, out GameObject prefab)
        {
            instance = null;
            prefab = null;
            int found = 0;
            using var data = new SerializedObject(controller);
            var characters = data.FindProperty("characters");
            for (int i = 0; i < characters.arraySize; i++)
            {
                var entry = characters.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("characterId").stringValue != id) continue;
                if (found++ > 0) continue;
                instance = entry.FindPropertyRelative("existingInstance").objectReferenceValue as GameObject;
                prefab = entry.FindPropertyRelative("characterPrefab").objectReferenceValue as GameObject;
            }
            return found;
        }

        private static RuntimeAnimatorController GetBaseController(RuntimeAnimatorController runtime)
        {
            while (runtime is AnimatorOverrideController wrapper) runtime = wrapper.runtimeAnimatorController;
            return runtime;
        }

        internal static IEnumerable<string> FindTriggers(RuntimeAnimatorController runtime) =>
            GetBaseController(runtime) is AnimatorController controller
                ? controller.parameters.Where(parameter => parameter.type == AnimatorControllerParameterType.Trigger).Select(parameter => parameter.name)
                : Array.Empty<string>();

        internal static void ShowAnimations(Rect rect, SerializedProperty action, CutsceneCharacterController controller)
        {
            var edit = Capture(action);
            Animator animator = ResolveAnimator(action, controller);
            RuntimeAnimatorController runtime = animator != null ? animator.runtimeAnimatorController : null;
            var states = FindStates(runtime);
            var items = new List<Item> { new("None", () => edit(p =>
            {
                p.FindPropertyRelative("animatorStateName").stringValue = string.Empty;
                p.FindPropertyRelative("animatorTrigger").stringValue = string.Empty;
            })) };
            foreach (var state in states)
                items.Add(new Item($"Playable state: {state.Path} (layer {state.Layer}) - {string.Join(", ", state.Clips.Select(c => c.name))}", () => edit(p =>
                {
                    p.FindPropertyRelative("animatorStateName").stringValue = state.Path;
                    p.FindPropertyRelative("animatorLayer").intValue = state.Layer;
                    p.FindPropertyRelative("animatorTrigger").stringValue = string.Empty;
                })));
            foreach (string trigger in FindTriggers(runtime))
                items.Add(new Item("Trigger: " + trigger, () => edit(p =>
                {
                    p.FindPropertyRelative("animatorTrigger").stringValue = trigger;
                    p.FindPropertyRelative("animatorStateName").stringValue = string.Empty;
                })));
            var mapped = new HashSet<AnimationClip>(states.SelectMany(state => state.Clips));
            foreach (string path in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(path => path))
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__") && !mapped.Contains(c)))
                    items.Add(new Item($"Project clip (not in this character's Animator): {clip.name} - {path}", null, false));
            if (runtime == null) items.Add(new Item("Assign a character with an Animator Controller to enable playable states.", null, false));
            new Search("Animations - search by state, clip, trigger, or asset path", items).Show(rect);
        }
    }

}
