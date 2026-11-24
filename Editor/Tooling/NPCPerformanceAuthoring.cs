using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Characters.NPC;
using QuietStatic.Toolkit.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Characters.NPC
{
    /// <summary>Previews and applies explicit edits to compatible, existing NPC components.</summary>
    internal static class NPCPerformanceAuthoring
    {
        internal enum Field
        {
            ConfiguredTrigger,
            LocomotionTriggers,
            LocomotionSpeedParameter,
            PauseDuringDialogue,
            FaceDuringDialogue,
            ReactionLookAtPlayer,
            ReactionStartAnimation,
            ReactionEndAnimation,
            ReactionStartMode,
            ReactionEndMode,
            StartupMode,
            BehaviorActiveOnStart
        }

        internal enum Behavior { LookAt, Follow, Wander, WaypointRoute }
        internal enum Status { Ready, Unchanged, Skipped, Applied, Failed }

        internal sealed class Patch
        {
            internal Field Field { get; }
            internal string Value { get; }
            internal string SecondaryValue { get; }
            internal bool Enabled { get; }
            internal Behavior BehaviorKind { get; }

            internal Patch(Field field, string value = "", string secondaryValue = "", bool enabled = false,
                Behavior behavior = Behavior.LookAt)
            {
                Field = field;
                Value = value?.Trim() ?? string.Empty;
                SecondaryValue = secondaryValue?.Trim() ?? string.Empty;
                Enabled = enabled;
                BehaviorKind = behavior;
            }
        }

        internal sealed class Row
        {
            public Component Target { get; }
            public string Current { get; }
            public string Proposed { get; }
            public string Detail { get; }
            public Status Status { get; }
            public bool CanApply => Status == Status.Ready;

            internal Row(Component target, string current, string proposed, string detail, Status status)
            {
                Target = target;
                Current = current;
                Proposed = proposed;
                Detail = detail;
                Status = status;
            }
        }

        private sealed class Plan
        {
            internal readonly Row Row;
            internal readonly Component Receiver;
            internal readonly Action<SerializedObject> SetValues;

            internal Plan(Row row, Component receiver = null, Action<SerializedObject> setValues = null)
            {
                Row = row;
                Receiver = receiver;
                SetValues = setValues;
            }
        }

        internal static IReadOnlyList<Row> Preview(IReadOnlyList<Component> targets, Patch patch) =>
            BuildPlans(targets, patch).Select(plan => plan.Row).ToArray();

        internal static IReadOnlyList<Row> Apply(IReadOnlyList<Component> targets, Patch patch)
        {
            Plan[] plans = BuildPlans(targets, patch);
            if (!plans.Any(plan => plan.Row.CanApply)) return plans.Select(plan => plan.Row).ToArray();

            const string undoName = "Edit NPC Performance";
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            var results = new List<Row>(plans.Length);
            try
            {
                foreach (Plan plan in plans)
                {
                    Row row = plan.Row;
                    if (!row.CanApply)
                    {
                        results.Add(row);
                        continue;
                    }

                    try
                    {
                        using var serialized = new SerializedObject(plan.Receiver);
                        plan.SetValues(serialized);
                        bool changed = serialized.ApplyModifiedProperties();
                        if (changed)
                        {
                            if (PrefabUtility.IsPartOfPrefabInstance(plan.Receiver))
                                PrefabUtility.RecordPrefabInstancePropertyModifications(plan.Receiver);
                            EditorSceneManager.MarkSceneDirty(plan.Receiver.gameObject.scene);
                        }

                        results.Add(new Row(row.Target, row.Current, row.Proposed, row.Detail,
                            changed ? Status.Applied : Status.Unchanged));
                    }
                    catch (Exception exception)
                    {
                        results.Add(new Row(row.Target, row.Current, row.Proposed, exception.Message, Status.Failed));
                    }
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            return results;
        }

        private static Plan[] BuildPlans(IReadOnlyList<Component> targets, Patch patch)
        {
            if (targets == null || patch == null) return Array.Empty<Plan>();

            return targets.Distinct().Select(target => BuildPlan(target, patch)).ToArray();
        }

        private static Plan BuildPlan(Component target, Patch patch)
        {
            string requested = Describe(patch);
            if (target == null) return Skip(target, "", requested, "The selected component no longer exists.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return Skip(target, "", requested, "Leave Play Mode to edit NPC performance settings.");

            if (EditorUtility.IsPersistent(target) || !target.gameObject.scene.IsValid() || !target.gameObject.scene.isLoaded)
                return Skip(target, "", requested, "Select an NPC in an open scene or open its prefab in Prefab Mode.");

            PrefabStage stage = PrefabStageUtility.GetPrefabStage(target.gameObject);
            if (stage != null && !AssetDatabase.IsOpenForEdit(stage.assetPath))
                return Skip(target, "", requested, "The prefab asset is read-only.");

            Type receiverType = ResolveReceiverType(patch);
            Component receiver = target;
            if (!receiverType.IsInstanceOfType(receiver))
            {
                Component[] candidates = target is NPCController ? target.GetComponents(receiverType) : Array.Empty<Component>();
                if (candidates.Length != 1)
                {
                    string label = ObjectNames.NicifyVariableName(receiverType.Name);
                    string detail = candidates.Length == 0 ? $"No {label} is assigned to this row." :
                        $"This NPC has several {label} components. Select a specific component.";
                    return Skip(target, "", requested, detail);
                }

                receiver = candidates[0];
            }

            if ((receiver.hideFlags & HideFlags.NotEditable) != 0 ||
                (receiver.gameObject.hideFlags & HideFlags.NotEditable) != 0)
                return Skip(target, "", requested, "The target component is read-only.");

            using var data = new SerializedObject(receiver);
            switch (patch.Field)
            {
                case Field.ConfiguredTrigger:
                    return PlanTrigger(target, receiver, data, "configuredTrigger", patch.Value, (NPCAnimationTrigger)receiver);
                case Field.LocomotionTriggers:
                case Field.LocomotionSpeedParameter:
                    return PlanLocomotion(target, receiver, data, patch);
                case Field.PauseDuringDialogue:
                    return PlanToggle(target, receiver, data, "stopMovementDuringDialogue", patch.Enabled);
                case Field.FaceDuringDialogue:
                    return PlanToggle(target, receiver, data, "faceControllerTargetDuringDialogue", patch.Enabled);
                case Field.StartupMode:
                    return PlanStartupMode(target, receiver, data, patch.Value);
                case Field.BehaviorActiveOnStart:
                    return PlanToggle(target, receiver, data, "activeOnStart", patch.Enabled, ModeOwnershipNote(receiver));
                default:
                    return PlanReaction(target, receiver, data, patch);
            }
        }

        private static Type ResolveReceiverType(Patch patch) => patch.Field switch
        {
            Field.ConfiguredTrigger => typeof(NPCAnimationTrigger),
            Field.LocomotionTriggers or Field.LocomotionSpeedParameter => typeof(NPCAnimatorDriver),
            Field.PauseDuringDialogue or Field.FaceDuringDialogue => typeof(NPCDialogueBehaviour),
            Field.StartupMode => typeof(NPCModeController),
            Field.BehaviorActiveOnStart => patch.BehaviorKind switch
            {
                Behavior.Wander => typeof(NPCWanderBehaviour),
                Behavior.Follow => typeof(NPCFollowBehaviour),
                Behavior.WaypointRoute => typeof(NPCWaypointRouteBehaviour),
                _ => typeof(NPCLookAtBehaviour)
            },
            _ => typeof(DialogueNPCReaction)
        };

        private static Plan PlanReaction(Component source, Component receiver, SerializedObject data, Patch patch)
        {
            var npc = data.FindProperty("npc").objectReferenceValue as NPCController;
            if (patch.Field == Field.ReactionLookAtPlayer)
            {
                if (patch.Enabled && (npc == null || npc.GetComponent<NPCLookAtBehaviour>() == null))
                    return Skip(source, Display(data.FindProperty("lookAtPlayer").boolValue), Display(patch.Enabled),
                        "The reaction needs an NPC with a Look At Behaviour.");

                return PlanToggle(source, receiver, data, "lookAtPlayer", patch.Enabled);
            }

            if (patch.Field == Field.ReactionStartAnimation || patch.Field == Field.ReactionEndAnimation)
            {
                string path = patch.Field == Field.ReactionStartAnimation ? "animationOnStart" : "animationOnEnd";
                return PlanTrigger(source, receiver, data, path, patch.Value,
                    npc != null ? npc.GetComponent<NPCAnimationTrigger>() : null);
            }

            string modePath = patch.Field == Field.ReactionStartMode ? "modeOnStart" : "modeOnEnd";
            string current = data.FindProperty(modePath).stringValue;
            if (patch.Value.Length == 0) return PlanString(source, receiver, modePath, current, string.Empty);

            NPCModeController mode = npc != null ? npc.GetComponent<NPCModeController>() : null;
            if (mode == null) return Skip(source, Display(current), Display(patch.Value), "The reaction needs an NPC Mode Controller.");

            using var modeData = new SerializedObject(mode);
            SerializedProperty modes = modeData.FindProperty("modes");
            if (!TryFindMode(modes, patch.Value, out int index, out string detail))
                return Skip(source, Display(current), Display(patch.Value), detail);

            return PlanString(source, receiver, modePath, current, ReadModeName(modes, index));
        }

        private static Plan PlanStartupMode(Component source, Component receiver, SerializedObject data, string name)
        {
            SerializedProperty modes = data.FindProperty("modes");
            int currentIndex = data.FindProperty("startingMode").intValue;
            string current = Display(ReadModeName(modes, currentIndex));
            if (!TryFindMode(modes, name, out int index, out string detail))
                return Skip(source, current, Display(name), detail);

            string note = StartupOwnershipNote(receiver);
            return MakePlan(source, receiver, current, Display(ReadModeName(modes, index)), note, currentIndex != index,
                serialized => serialized.FindProperty("startingMode").intValue = index);
        }

        private static bool TryFindMode(SerializedProperty modes, string name, out int index, out string detail)
        {
            index = -1;
            if (name.Length == 0)
            {
                detail = "Enter an existing mode name.";
                return false;
            }

            for (int item = 0; item < modes.arraySize; item++)
            {
                if (!string.Equals(ReadModeName(modes, item), name, StringComparison.OrdinalIgnoreCase)) continue;
                if (index >= 0)
                {
                    detail = $"Several modes are named '{name}'.";
                    return false;
                }

                index = item;
            }

            detail = index < 0 ? $"No NPC mode is named '{name}'." : string.Empty;
            return index >= 0;
        }

        private static string ReadModeName(SerializedProperty modes, int index) =>
            index >= 0 && index < modes.arraySize
                ? modes.GetArrayElementAtIndex(index).FindPropertyRelative("modeName").stringValue
                : string.Empty;

        private static Plan PlanTrigger(Component source, Component receiver, SerializedObject data, string path,
            string name, NPCAnimationTrigger adapter)
        {
            string current = data.FindProperty(path).stringValue;
            if (name.Length != 0)
            {
                string error = ParameterError(NPCInspectorUtility.ResolveAnimator(adapter), name, AnimatorControllerParameterType.Trigger);
                if (error.Length != 0) return Skip(source, Display(current), Display(name), error);
            }

            return PlanString(source, receiver, path, current, name);
        }

        private static Plan PlanLocomotion(Component source, Component receiver, SerializedObject data, Patch patch)
        {
            string moving = data.FindProperty("movingTrigger").stringValue;
            string stationary = data.FindProperty("stationaryTrigger").stringValue;
            string speed = data.FindProperty("speedParameter").stringValue;
            string current = !string.IsNullOrWhiteSpace(moving) && !string.IsNullOrWhiteSpace(stationary)
                ? DescribeTriggers(moving, stationary) : $"Float · {Display(speed)}";
            string proposed = Describe(patch);
            Animator animator = NPCInspectorUtility.ResolveAnimator(receiver);
            if (patch.Field == Field.LocomotionTriggers &&
                (patch.Value.Length == 0 || patch.SecondaryValue.Length == 0 || patch.Value == patch.SecondaryValue))
                return Skip(source, current, proposed, "Choose two different Animator triggers for moving and stationary.");

            bool useTriggers = patch.Field == Field.LocomotionTriggers;
            string error = ParameterError(animator, patch.Value,
                useTriggers ? AnimatorControllerParameterType.Trigger : AnimatorControllerParameterType.Float);
            if (error.Length == 0 && useTriggers)
                error = ParameterError(animator, patch.SecondaryValue, AnimatorControllerParameterType.Trigger);
            if (error.Length != 0) return Skip(source, current, proposed, error);

            if (HasMotorAnimationEvents(receiver, data, animator))
                return Skip(source, current, proposed, "The motor already has motion events that target this Animator.");

            string newMoving = useTriggers ? patch.Value : string.Empty;
            string newStationary = useTriggers ? patch.SecondaryValue : string.Empty;
            string newSpeed = useTriggers ? speed : patch.Value;
            return MakePlan(source, receiver, current, proposed, "",
                moving != newMoving || stationary != newStationary || speed != newSpeed, serialized =>
                {
                    serialized.FindProperty("movingTrigger").stringValue = newMoving;
                    serialized.FindProperty("stationaryTrigger").stringValue = newStationary;
                    serialized.FindProperty("speedParameter").stringValue = newSpeed;
                });
        }

        private static string ParameterError(Animator animator, string name, AnimatorControllerParameterType type)
        {
            if (name.Length == 0) return $"Enter an Animator {type} parameter name.";
            if (animator == null) return "No Animator is assigned or discoverable.";
            if (animator.runtimeAnimatorController == null) return "The Animator has no controller.";

            var parameters = NPCInspectorUtility.ReadAnimatorParameters(animator);
            return parameters.Any(parameter => parameter.name == name && parameter.type == type)
                ? string.Empty : $"The Animator controller has no {type} parameter named '{name}'.";
        }

        private static bool HasMotorAnimationEvents(Component receiver, SerializedObject driver, Animator animator)
        {
            NPCNavMeshMotor motor = driver.FindProperty("motor").objectReferenceValue as NPCNavMeshMotor;
            if (motor == null) motor = receiver.GetComponent<NPCNavMeshMotor>();
            if (motor == null) return false;

            using var data = new SerializedObject(motor);
            return HasPersistentCall(data, "onStartedMoving", AffectsAnimator) || HasPersistentCall(data, "onStoppedMoving", AffectsAnimator);

            bool AffectsAnimator(UnityEngine.Object called, string method) =>
                (called == animator && method.StartsWith("Set", StringComparison.Ordinal)) ||
                (called == animator && (method == "CrossFade" || method == "Play" || method == "ResetTrigger")) ||
                (called is NPCAnimationTrigger adapter && (method == "SetTrigger" || method == "ResetTrigger" || method == "TriggerConfigured") &&
                 NPCInspectorUtility.ResolveAnimator(adapter) == animator);
        }

        private static string ModeOwnershipNote(Component behavior)
        {
            var mode = behavior.GetComponent<NPCModeController>();
            if (mode == null) return string.Empty;

            using var data = new SerializedObject(mode);
            SerializedProperty managed = data.FindProperty("managedBehaviours");
            for (int index = 0; index < managed.arraySize; index++)
                if (managed.GetArrayElementAtIndex(index).objectReferenceValue == behavior)
                    return "The NPC's startup mode also controls this behavior.";

            return string.Empty;
        }

        private static string StartupOwnershipNote(Component mode)
        {
            var startup = mode.GetComponent<NPCStartupBehaviour>();
            if (startup == null) return string.Empty;

            using var data = new SerializedObject(startup);
            return HasPersistentCall(data, "onStartup", (target, method) => target == mode && (method == "SetMode" || method == "SetModeByName"))
                ? "This NPC also sets its mode through a startup event." : string.Empty;
        }

        private static bool HasPersistentCall(SerializedObject data, string property, Func<UnityEngine.Object, string, bool> matches)
        {
            SerializedProperty calls = data.FindProperty(property + ".m_PersistentCalls.m_Calls");
            if (calls == null) return false;

            for (int index = 0; index < calls.arraySize; index++)
            {
                SerializedProperty call = calls.GetArrayElementAtIndex(index);
                if (call.FindPropertyRelative("m_CallState").intValue == (int)UnityEngine.Events.UnityEventCallState.Off) continue;
                if (matches(call.FindPropertyRelative("m_Target").objectReferenceValue,
                        call.FindPropertyRelative("m_MethodName").stringValue)) return true;
            }

            return false;
        }

        private static Plan PlanToggle(Component source, Component receiver, SerializedObject data, string path, bool enabled, string detail = "")
        {
            bool current = data.FindProperty(path).boolValue;
            return MakePlan(source, receiver, Display(current), Display(enabled), detail, current != enabled,
                serialized => serialized.FindProperty(path).boolValue = enabled);
        }

        private static Plan PlanString(Component source, Component receiver, string path, string current, string value) =>
            MakePlan(source, receiver, Display(current), Display(value), "", current != value,
                serialized => serialized.FindProperty(path).stringValue = value);

        private static Plan MakePlan(Component source, Component receiver, string current, string proposed, string detail,
            bool changed, Action<SerializedObject> setValues) =>
            new(new Row(source, current, proposed, detail, changed ? Status.Ready : Status.Unchanged), receiver, setValues);

        private static Plan Skip(Component target, string current, string proposed, string detail) =>
            new(new Row(target, current, proposed, detail, Status.Skipped));

        private static string Describe(Patch patch) => patch.Field switch
        {
            Field.LocomotionTriggers => DescribeTriggers(patch.Value, patch.SecondaryValue),
            Field.LocomotionSpeedParameter => $"Float · {Display(patch.Value)}",
            Field.PauseDuringDialogue or Field.FaceDuringDialogue or Field.ReactionLookAtPlayer or Field.BehaviorActiveOnStart => Display(patch.Enabled),
            _ => Display(patch.Value)
        };

        private static string DescribeTriggers(string moving, string stationary) => $"Triggers · {Display(moving)} / {Display(stationary)}";
        private static string Display(string value) => string.IsNullOrWhiteSpace(value) ? "None" : value;
        private static string Display(bool value) => value ? "On" : "Off";
    }
}
