using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Minigames;
using QuietStatic.Toolkit.Objectives;
using QuietStatic.Toolkit.Spawning;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.Validation
{
    /// <summary>Severity assigned to a toolkit validation result.</summary>
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>A navigable, read-only issue produced by a validation rule.</summary>
    public sealed class ValidationIssue
    {
        public ValidationIssue(
            ValidationSeverity severity,
            string category,
            string message,
            UnityEngine.Object context = null,
            string assetPath = null)
        {
            Severity = severity;
            Category = category;
            Message = message;
            Context = context;
            AssetPath = assetPath;
        }

        public ValidationSeverity Severity { get; }
        public string Category { get; }
        public string Message { get; }
        public UnityEngine.Object Context { get; }
        public string AssetPath { get; }
    }

    /// <summary>
    /// Project and open-scene validation rules shared by the narrative and scene windows.
    /// Rules deliberately report issues without modifying content.
    /// </summary>
    public static class ToolkitValidation
    {
        public static IReadOnlyList<ValidationIssue> ScanNarrative()
        {
            var issues = new List<ValidationIssue>();
            FlagDatabase[] databases = FindAssets<FlagDatabase>();
            var knownFlags = new HashSet<string>(StringComparer.Ordinal);

            foreach (FlagDatabase database in databases)
            {
                ValidateFlagDatabase(database, knownFlags, issues);
            }

            foreach (ObjectiveDatabase database in FindAssets<ObjectiveDatabase>())
            {
                ValidateObjectiveDatabase(
                    database,
                    knownFlags,
                    databases.Length > 0,
                    issues);
            }

            foreach (DialogueTree tree in FindAssets<DialogueTree>())
            {
                ValidateDialogue(tree, knownFlags, databases.Length > 0, issues);
            }

            foreach (Component component in FindOpenSceneComponents())
            {
                if (component is ObjectiveResolver objective)
                {
                    ValidateObjective(objective, knownFlags, databases.Length > 0, issues);
                }
                else if (component is ObjectiveManager objectiveManager &&
                         objectiveManager.Database == null)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Warning,
                        "Objectives",
                        "Objective Manager has no Objective Database, so active objectives cannot be restored from save data.",
                        objectiveManager));
                }
                else if (component is CutsceneSequenceRunner cutscene)
                {
                    ValidateCutscene(cutscene, issues);
                }
            }

            ValidateFlagReferences(knownFlags, databases.Length > 0, issues);
            return issues;
        }

        public static IReadOnlyList<ValidationIssue> ScanOpenScenes()
        {
            var issues = new List<ValidationIssue>();
            Component[] components = FindOpenSceneComponents().ToArray();
            GameStateDatabase[] stateDatabases =
                FindAssets<GameStateDatabase>();
            var knownStates = new HashSet<string>(StringComparer.Ordinal);

            foreach (GameStateDatabase database in stateDatabases)
            {
                ValidateGameStateDatabase(database, knownStates, issues);
            }

            ValidateGameStateReferences(
                knownStates,
                stateDatabases.Length > 0,
                issues);

            foreach (Component component in components.Where(item => item == null))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error, "Missing Script",
                    "An open scene contains a missing MonoBehaviour script."));
            }

            foreach (IGrouping<Type, Component> group in components
                         .Where(component => component != null &&
                                             IsToolkitManager(component.GetType()))
                         .GroupBy(component => component.GetType())
                         .Where(group => group.Count() > 1))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error, "Managers",
                    $"Open scenes contain {group.Count()} {group.Key.Name} components; persistent managers must be unique.",
                    group.First()));
            }

            int listeners = components.Count(component => component is AudioListener);
            if (listeners == 0)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning, "Audio",
                    "No enabled or disabled AudioListener exists in the open scenes."));
            }
            else if (listeners > 1)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning, "Audio",
                    $"Open scenes contain {listeners} AudioListeners. Only one should be active at runtime.",
                    components.First(component => component is AudioListener)));
            }

            if (!components.Any(component => component is EventSystem))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Info, "UI",
                    "No EventSystem exists in the open scenes. This is valid when UI is loaded additively."));
            }

            ValidateSpawning(components, issues);
            ValidateMinigames(components, issues);

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                string path = AssetDatabase.GUIDToAssetPath(scene.guid);
                if (scene.enabled &&
                    (string.IsNullOrEmpty(path) ||
                     AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, "Build Settings",
                        $"Enabled build scene is missing (GUID {scene.guid})."));
                }
            }

            return issues;
        }

        private static void ValidateMinigames(
            IEnumerable<Component> components,
            ICollection<ValidationIssue> issues)
        {
            foreach (InputSequenceDefinition definition
                         in FindAssets<InputSequenceDefinition>())
            {
                if (definition.Count == 0)
                {
                    issues.Add(Issue(
                        ValidationSeverity.Error,
                        "Minigames",
                        "Input sequence has no steps.",
                        definition));
                    continue;
                }

                for (int index = 0; index < definition.Count; index++)
                {
                    if (definition.Steps[index]?.Action == null)
                    {
                        issues.Add(Issue(
                            ValidationSeverity.Error,
                            "Minigames",
                            $"Input sequence step {index + 1} has no action.",
                            definition));
                    }
                }
            }

            InputSequenceMinigame[] runners = components
                .OfType<InputSequenceMinigame>()
                .ToArray();
            foreach (InputSequenceMinigame runner in runners)
            {
                var serialized = new SerializedObject(runner);
                UnityEngine.Object definition =
                    serialized.FindProperty("sequence")?.objectReferenceValue;
                UnityEngine.Object channel =
                    serialized.FindProperty("requestChannel")?.objectReferenceValue;
                if (definition == null && channel == null)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Minigames",
                        "Input Sequence Minigame needs a default sequence or request channel.",
                        runner));
                }

                InputSequenceView view =
                    serialized.FindProperty("view")?.objectReferenceValue
                        as InputSequenceView;
                if (view != null)
                {
                    var viewData = new SerializedObject(view);
                    GameObject displayRoot =
                        viewData.FindProperty("displayRoot")?.objectReferenceValue
                            as GameObject;
                    if (displayRoot == runner.gameObject)
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error,
                            "Minigames",
                            "The minigame view display root cannot be the runner GameObject because hiding it disables the runner.",
                            runner));
                    }
                }
            }

            foreach (IGrouping<UnityEngine.Object, InputSequenceMinigame> group
                         in runners
                             .Select(runner => new
                             {
                                 Runner = runner,
                                 Channel = new SerializedObject(runner)
                                     .FindProperty("requestChannel")
                                     ?.objectReferenceValue
                             })
                             .Where(item => item.Channel != null)
                             .GroupBy(item => item.Channel, item => item.Runner)
                             .Where(group => group.Count() > 1))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    "Minigames",
                    $"Open scenes contain {group.Count()} runners listening to the same minigame request channel.",
                    group.First()));
            }

            foreach (InputSequenceMinigameActivator activator
                         in components.OfType<InputSequenceMinigameActivator>())
            {
                var serialized = new SerializedObject(activator);
                if (serialized.FindProperty("sequence")?.objectReferenceValue == null)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Minigames",
                        "Input Sequence Minigame Activator has no sequence.",
                        activator));
                }

                if (serialized.FindProperty("localRunner")?.objectReferenceValue == null &&
                    serialized.FindProperty("requestChannel")?.objectReferenceValue == null)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Minigames",
                        "Input Sequence Minigame Activator needs a local runner or request channel.",
                    activator));
                }
            }

            foreach (InputSequenceMinigameTrigger trigger
                         in components.OfType<InputSequenceMinigameTrigger>())
            {
                var serialized = new SerializedObject(trigger);
                if (serialized.FindProperty("activator")?.objectReferenceValue == null &&
                    serialized.FindProperty("minigame")?.objectReferenceValue == null)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Minigames",
                        "Input Sequence Minigame Trigger needs an activator or local runner.",
                        trigger));
                }
            }
        }

        private static void ValidateSpawning(
            IEnumerable<Component> components,
            ICollection<ValidationIssue> issues)
        {
            SpawnPoint[] spawnPoints = components
                .OfType<SpawnPoint>()
                .ToArray();
            SpawnTarget[] spawnTargets = components
                .OfType<SpawnTarget>()
                .ToArray();

            foreach (SpawnPoint spawnPoint in spawnPoints
                         .Where(point => string.IsNullOrWhiteSpace(point.Id)))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Spawning",
                    "Spawn Point has no stable ID.",
                    spawnPoint));
            }

            foreach (IGrouping<string, SpawnPoint> duplicate in spawnPoints
                         .Where(point => !string.IsNullOrWhiteSpace(point.Id))
                         .GroupBy(point => point.Id.Trim(), StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Spawning",
                    $"Open scenes contain {duplicate.Count()} spawn points with ID '{duplicate.Key}'.",
                    duplicate.First()));
            }

            foreach (SpawnTarget spawnTarget in spawnTargets
                         .Where(target =>
                             string.IsNullOrWhiteSpace(target.TargetId)))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Spawning",
                    "Spawn Target has no stable target ID.",
                    spawnTarget));
            }

            foreach (IGrouping<string, SpawnTarget> duplicate in spawnTargets
                         .Where(target =>
                             !string.IsNullOrWhiteSpace(target.TargetId))
                         .GroupBy(
                             target => target.TargetId,
                             StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Spawning",
                    $"Open scenes contain {duplicate.Count()} spawn targets with ID '{duplicate.Key}'.",
                    duplicate.First()));
            }

            if (spawnTargets.Length > 0 &&
                !components.Any(component => component is SpawnManager))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    "Spawning",
                    "Spawn Targets exist without a Spawn Manager in the open scenes.",
                    spawnTargets[0]));
            }
        }

        private static void ValidateGameStateDatabase(
            GameStateDatabase database,
            HashSet<string> knownStates,
            ICollection<ValidationIssue> issues)
        {
            var local = new HashSet<string>(StringComparer.Ordinal);
            GameStateDatabase.StateDefinition[] definitions =
                database.States ??
                Array.Empty<GameStateDatabase.StateDefinition>();

            for (int index = 0; index < definitions.Length; index++)
            {
                string id = definitions[index]?.state?.Trim();
                if (string.IsNullOrEmpty(id))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Game States",
                        $"Game-state entry {index} has an empty identifier.",
                        database,
                        AssetDatabase.GetAssetPath(database)));
                    continue;
                }

                if (!local.Add(id) || !knownStates.Add(id))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Game States",
                        $"Duplicate game-state identifier '{id}'.",
                        database,
                        AssetDatabase.GetAssetPath(database)));
                }
            }
        }

        private static void ValidateGameStateReferences(
            HashSet<string> knownStates,
            bool hasDatabase,
            ICollection<ValidationIssue> issues)
        {
            if (!hasDatabase)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Info,
                    "Game States",
                    "No Game State Database was found; unknown state checks were skipped."));
                return;
            }

            foreach (UnityEngine.Object candidate in EnumerateSerializedCandidates())
            {
                if (candidate is GameStateDatabase)
                {
                    continue;
                }

                ScanStringProperties(
                    candidate,
                    (property, value) =>
                    {
                        if (!string.IsNullOrWhiteSpace(value) &&
                            !knownStates.Contains(value.Trim()))
                        {
                            issues.Add(new ValidationIssue(
                                ValidationSeverity.Error,
                                "Game States",
                                $"Unknown game-state ID '{value}' at " +
                                $"{property.propertyPath}.",
                                candidate,
                                AssetDatabase.GetAssetPath(candidate)));
                        }
                    },
                    IsGameStateProperty);
            }
        }

        /// <summary>Finds serialized project and open-scene fields likely to store a flag ID.</summary>
        public static IReadOnlyList<ValidationIssue> FindFlagReferences(string flagId)
        {
            var results = new List<ValidationIssue>();
            if (string.IsNullOrWhiteSpace(flagId))
            {
                return results;
            }

            foreach (UnityEngine.Object candidate in EnumerateSerializedCandidates())
            {
                ScanStringProperties(candidate, (property, value) =>
                {
                    if (string.Equals(value, flagId, StringComparison.Ordinal))
                    {
                        results.Add(new ValidationIssue(
                            ValidationSeverity.Info, "Flag Reference",
                            $"{candidate.name}: {property.propertyPath}",
                            candidate, AssetDatabase.GetAssetPath(candidate)));
                    }
                });
            }

            return results;
        }

        private static void ValidateFlagDatabase(
            FlagDatabase database,
            HashSet<string> knownFlags,
            ICollection<ValidationIssue> issues)
        {
            var local = new HashSet<string>(StringComparer.Ordinal);
            FlagDatabase.FlagDefinition[] definitions = database.Flags ?? Array.Empty<FlagDatabase.FlagDefinition>();
            for (int index = 0; index < definitions.Length; index++)
            {
                string id = definitions[index]?.id?.Trim();
                if (string.IsNullOrEmpty(id))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, "Flags",
                        $"Flag entry {index} has an empty identifier.", database,
                        AssetDatabase.GetAssetPath(database)));
                    continue;
                }

                if (!local.Add(id) || !knownFlags.Add(id))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, "Flags",
                        $"Duplicate flag identifier '{id}'.", database,
                        AssetDatabase.GetAssetPath(database)));
                }
            }
        }

        private static void ValidateDialogue(
            DialogueTree tree,
            HashSet<string> knownFlags,
            bool validateFlags,
            ICollection<ValidationIssue> issues)
        {
            DialogueTree.Node[] nodes = tree.Nodes ?? Array.Empty<DialogueTree.Node>();
            if (nodes.Length == 0)
            {
                issues.Add(Issue(ValidationSeverity.Error, "Dialogue", "Dialogue has no nodes.", tree));
                return;
            }

            if (tree.StartNodeIndex < 0 || tree.StartNodeIndex >= nodes.Length)
            {
                issues.Add(Issue(ValidationSeverity.Error, "Dialogue",
                    $"Start node index {tree.StartNodeIndex} is outside the node array.", tree));
            }

            var reachable = new HashSet<int>();
            VisitDialogue(tree.StartNodeIndex, nodes, reachable);
            for (int index = 0; index < nodes.Length; index++)
            {
                DialogueTree.Node node = nodes[index];
                if (node == null)
                {
                    issues.Add(Issue(ValidationSeverity.Error, "Dialogue", $"Node {index} is null.", tree));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.line))
                {
                    issues.Add(Issue(ValidationSeverity.Warning, "Dialogue", $"Node {index} has empty dialogue text.", tree));
                }

                ValidateTarget(node.nextNodeIndex, nodes.Length, $"Node {index}", tree, issues);
                ValidateFlags(node.flagsToSetOnEnter, knownFlags, validateFlags, $"Node {index}", tree, issues);

                if (node.choices != null)
                {
                    for (int choiceIndex = 0; choiceIndex < node.choices.Length; choiceIndex++)
                    {
                        DialogueTree.Choice choice = node.choices[choiceIndex];
                        if (choice == null)
                        {
                            issues.Add(Issue(ValidationSeverity.Error, "Dialogue",
                                $"Node {index}, choice {choiceIndex} is null.", tree));
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(choice.text))
                        {
                            issues.Add(Issue(ValidationSeverity.Warning, "Dialogue",
                                $"Node {index}, choice {choiceIndex} has empty text.", tree));
                        }

                        ValidateTarget(choice.nextNodeIndex, nodes.Length,
                            $"Node {index}, choice {choiceIndex}", tree, issues);
                        ValidateFlags(choice.flagsToSet, knownFlags, validateFlags,
                            $"Node {index}, choice {choiceIndex}", tree, issues);
                        if (choice.availabilityRequirement != null)
                        {
                            ValidateFlags(
                                choice.availabilityRequirement.Flags.ToArray(),
                                knownFlags,
                                validateFlags,
                                $"Node {index}, choice {choiceIndex} condition",
                                tree,
                                issues);
                        }
                    }
                }

                if (!reachable.Contains(index))
                {
                    issues.Add(Issue(ValidationSeverity.Warning, "Dialogue",
                        $"Node {index} is unreachable from start node {tree.StartNodeIndex}.", tree));
                }
            }
        }

        private static void ValidateObjective(
            ObjectiveResolver objective,
            HashSet<string> knownFlags,
            bool validateFlags,
            ICollection<ValidationIssue> issues)
        {
            var serialized = new SerializedObject(objective);
            SerializedProperty entries = serialized.FindProperty("objectives");
            for (int index = 0; entries != null && index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                ObjectiveDefinition definition =
                    entry.FindPropertyRelative("definition")?.objectReferenceValue
                        as ObjectiveDefinition;
                if (definition == null &&
                    string.IsNullOrWhiteSpace(
                        entry.FindPropertyRelative("objectiveText")?.stringValue))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Warning, "Objectives",
                        $"Objective entry {index} has no definition or legacy display text.",
                        objective));
                }

                SerializedProperty requirement = entry.FindPropertyRelative("requirement");
                SerializedProperty mode = requirement?.FindPropertyRelative("mode");
                SerializedProperty flags = requirement?.FindPropertyRelative("flags");
                if (mode != null && mode.enumValueIndex != 0 && (flags == null || flags.arraySize == 0))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Warning, "Objectives",
                        $"Objective entry {index} has a requirement mode but no flags.", objective));
                }

                ValidateSerializedFlags(flags, knownFlags, validateFlags,
                    $"Objective entry {index}", objective, issues);
            }
        }

        private static void ValidateObjectiveDatabase(
            ObjectiveDatabase database,
            HashSet<string> knownFlags,
            bool validateFlags,
            ICollection<ValidationIssue> issues)
        {
            if (database == null)
            {
                return;
            }

            var knownObjectiveIds = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < database.Objectives.Count; index++)
            {
                ObjectiveDefinition objective = database.Objectives[index];

                if (objective == null)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Objectives",
                        $"Objective Database entry {index} is missing a definition.",
                        database));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(objective.Id))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Objectives",
                        $"Objective '{objective.name}' has no stable ID.",
                        objective));
                }
                else if (!knownObjectiveIds.Add(objective.Id))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Objectives",
                        $"Objective Database contains duplicate ID '{objective.Id}'.",
                        objective));
                }

                if (string.IsNullOrWhiteSpace(objective.DisplayText))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Warning,
                        "Objectives",
                        $"Objective '{objective.name}' has no title or description.",
                        objective));
                }

                var serialized = new SerializedObject(objective);
                SerializedProperty requirement =
                    serialized.FindProperty("completionRequirement");
                SerializedProperty mode =
                    requirement?.FindPropertyRelative("mode");
                SerializedProperty flags =
                    requirement?.FindPropertyRelative("flags");

                if (mode != null &&
                    mode.enumValueIndex != 0 &&
                    (flags == null || flags.arraySize == 0))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Warning,
                        "Objectives",
                        $"Objective '{objective.name}' has a completion mode but no flags.",
                        objective));
                }

                ValidateSerializedFlags(
                    flags,
                    knownFlags,
                    validateFlags,
                    $"Objective '{objective.name}' completion",
                    objective,
                    issues);
            }
        }

        private static void ValidateCutscene(
            CutsceneSequenceRunner cutscene,
            ICollection<ValidationIssue> issues)
        {
            var serialized = new SerializedObject(cutscene);
            SerializedProperty steps = serialized.FindProperty("steps");
            if (steps == null || steps.arraySize == 0)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning, "Cutscenes",
                    "Cutscene sequence has no steps.", cutscene));
                return;
            }

            for (int index = 0; index < steps.arraySize; index++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(index);
                bool hasCamera = step.FindPropertyRelative("cameraTransform")?.objectReferenceValue != null;
                bool hasPose = step.FindPropertyRelative("cameraPose")?.objectReferenceValue != null;
                if (hasCamera != hasPose)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, "Cutscenes",
                        $"Step {index} must assign both Camera Transform and Camera Pose.", cutscene));
                }
            }
        }

        private static void ValidateFlagReferences(
            HashSet<string> knownFlags,
            bool validateFlags,
            ICollection<ValidationIssue> issues)
        {
            if (!validateFlags)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Info, "Flags",
                    "No Flag Database was found; unknown flag checks were skipped."));
                return;
            }

            foreach (UnityEngine.Object candidate in EnumerateSerializedCandidates())
            {
                ScanStringProperties(candidate, (property, value) =>
                {
                    if (!string.IsNullOrWhiteSpace(value) &&
                        IsFlagProperty(property) &&
                        !knownFlags.Contains(value.Trim()))
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error, "Flags",
                            $"Unknown flag '{value}' at {property.propertyPath}.",
                            candidate, AssetDatabase.GetAssetPath(candidate)));
                    }
                });
            }
        }

        private static void ValidateTarget(
            int target,
            int count,
            string owner,
            DialogueTree tree,
            ICollection<ValidationIssue> issues)
        {
            if (target < -1 || target >= count)
            {
                issues.Add(Issue(ValidationSeverity.Error, "Dialogue",
                    $"{owner} points to invalid node index {target}.", tree));
            }
        }

        private static void VisitDialogue(int index, DialogueTree.Node[] nodes, ISet<int> visited)
        {
            if (index < 0 || index >= nodes.Length || !visited.Add(index) || nodes[index] == null)
            {
                return;
            }

            DialogueTree.Node node = nodes[index];
            VisitDialogue(node.nextNodeIndex, nodes, visited);
            if (node.choices == null)
            {
                return;
            }

            foreach (DialogueTree.Choice choice in node.choices)
            {
                if (choice != null)
                {
                    VisitDialogue(choice.nextNodeIndex, nodes, visited);
                }
            }
        }

        private static void ValidateFlags(
            IEnumerable<string> flags,
            HashSet<string> known,
            bool enabled,
            string owner,
            UnityEngine.Object context,
            ICollection<ValidationIssue> issues)
        {
            if (!enabled || flags == null)
            {
                return;
            }

            foreach (string flag in flags.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                if (!known.Contains(flag.Trim()))
                {
                    issues.Add(Issue(ValidationSeverity.Error, "Flags",
                        $"{owner} references unknown flag '{flag}'.", context));
                }
            }
        }

        private static void ValidateSerializedFlags(
            SerializedProperty flags,
            HashSet<string> known,
            bool enabled,
            string owner,
            UnityEngine.Object context,
            ICollection<ValidationIssue> issues)
        {
            if (!enabled || flags == null || !flags.isArray)
            {
                return;
            }

            for (int index = 0; index < flags.arraySize; index++)
            {
                string value = flags.GetArrayElementAtIndex(index).stringValue;
                ValidateFlags(new[] { value }, known, true, owner, context, issues);
            }
        }

        private static ValidationIssue Issue(
            ValidationSeverity severity,
            string category,
            string message,
            UnityEngine.Object context)
        {
            return new ValidationIssue(severity, category, message, context,
                AssetDatabase.GetAssetPath(context));
        }

        private static T[] FindAssets<T>() where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToArray();
        }

        private static IEnumerable<Component> FindOpenSceneComponents()
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Component component in root.GetComponentsInChildren<Component>(true))
                    {
                        yield return component;
                    }
                }
            }
        }

        private static IEnumerable<UnityEngine.Object> EnumerateSerializedCandidates()
        {
            var seen = new HashSet<int>();
            foreach (string path in AssetDatabase.GetAllAssetPaths()
                         .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) &&
                                        (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                                         path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))))
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null || asset is FlagDatabase)
                {
                    continue;
                }

                if (asset is GameObject prefab)
                {
                    foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
                    {
                        if (component != null && seen.Add(component.GetInstanceID()))
                        {
                            yield return component;
                        }
                    }
                }
                else if (seen.Add(asset.GetInstanceID()))
                {
                    yield return asset;
                }
            }

            foreach (Component component in FindOpenSceneComponents())
            {
                if (component != null && seen.Add(component.GetInstanceID()))
                {
                    yield return component;
                }
            }
        }

        private static void ScanStringProperties(
            UnityEngine.Object candidate,
            Action<SerializedProperty, string> visitor,
            Func<SerializedProperty, bool> predicate = null)
        {
            try
            {
                var serialized = new SerializedObject(candidate);
                SerializedProperty property = serialized.GetIterator();
                if (!property.NextVisible(true))
                {
                    return;
                }

                do
                {
                    if (property.propertyType == SerializedPropertyType.String &&
                        (predicate ?? IsFlagProperty)(property))
                    {
                        visitor(property.Copy(), property.stringValue);
                    }
                } while (property.NextVisible(true));
            }
            catch (Exception)
            {
                // Some importer-owned objects cannot safely create a SerializedObject.
            }
        }

        private static bool IsFlagProperty(SerializedProperty property)
        {
            return property.name.IndexOf("flag", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   property.propertyPath.IndexOf("flag", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsGameStateProperty(SerializedProperty property)
        {
            string path = property.propertyPath;
            return property.name == "startingState" ||
                   property.name == "gameplayState" ||
                   property.name == "pausedState" ||
                   property.name == "stateId" ||
                   path.IndexOf("gameplayStates", StringComparison.Ordinal) >= 0 ||
                   path.IndexOf("uiStates", StringComparison.Ordinal) >= 0 ||
                   path.IndexOf("cutsceneStates", StringComparison.Ordinal) >= 0 ||
                   path.IndexOf("visibleStates", StringComparison.Ordinal) >= 0;
        }

        private static bool IsToolkitManager(Type type)
        {
            return type.Namespace != null &&
                   type.Namespace.StartsWith("QuietStatic", StringComparison.Ordinal) &&
                   type.Name.EndsWith("Manager", StringComparison.Ordinal);
        }
    }
}
