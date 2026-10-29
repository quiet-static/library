using System;
using System.Linq;
using QuietStatic.Toolkit.Characters.NPC;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Interactions;
using QuietStatic.Toolkit.Interactions;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace QuietStatic.Toolkit.Editor.Setup
{
    /// <summary>Additive, undoable component bundles for selected scene objects.</summary>
    public static class GameObjectSetupMenu
    {
        private const string Root = "GameObject/Quiet Static/Attach Setup/";

        /// <summary>Common toolkit compositions available from the Hierarchy context menu.</summary>
        public enum SetupKind
        {
            BasicInteraction, FlavorText, ConditionalMessage, Dialogue, Readable, Animated, Audio,
            Hold, TimedProgress, StationaryNPC, DialogueNPC, FollowNPC, WanderNPC, WaypointNPC, SceneExit
        }

        [MenuItem(Root + "Interactions/Basic", false, 10)] private static void Basic(MenuCommand c) => Apply(c, SetupKind.BasicInteraction);
        [MenuItem(Root + "Interactions/Flavor Text", false, 11)] private static void Flavor(MenuCommand c) => Apply(c, SetupKind.FlavorText);
        [MenuItem(Root + "Interactions/Conditional Message", false, 12)] private static void Conditional(MenuCommand c) => Apply(c, SetupKind.ConditionalMessage);
        [MenuItem(Root + "Interactions/Dialogue", false, 13)] private static void Dialogue(MenuCommand c) => Apply(c, SetupKind.Dialogue);
        [MenuItem(Root + "Interactions/Readable", false, 14)] private static void Readable(MenuCommand c) => Apply(c, SetupKind.Readable);
        [MenuItem(Root + "Interactions/Animated", false, 15)] private static void Animated(MenuCommand c) => Apply(c, SetupKind.Animated);
        [MenuItem(Root + "Interactions/Audio", false, 16)] private static void Audio(MenuCommand c) => Apply(c, SetupKind.Audio);
        [MenuItem(Root + "Interactions/Hold", false, 17)] private static void Hold(MenuCommand c) => Apply(c, SetupKind.Hold);
        [MenuItem(Root + "Interactions/Timed Progress", false, 18)] private static void Progress(MenuCommand c) => Apply(c, SetupKind.TimedProgress);
        [MenuItem(Root + "NPCs/Stationary", false, 30)] private static void NPC(MenuCommand c) => Apply(c, SetupKind.StationaryNPC);
        [MenuItem(Root + "NPCs/Dialogue with Resolver", false, 31)] private static void NPCDialogue(MenuCommand c) => Apply(c, SetupKind.DialogueNPC);
        [MenuItem(Root + "NPCs/Follow", false, 32)] private static void Follow(MenuCommand c) => Apply(c, SetupKind.FollowNPC);
        [MenuItem(Root + "NPCs/Wander", false, 33)] private static void Wander(MenuCommand c) => Apply(c, SetupKind.WanderNPC);
        [MenuItem(Root + "NPCs/Waypoint Route", false, 34)] private static void Route(MenuCommand c) => Apply(c, SetupKind.WaypointNPC);
        [MenuItem(Root + "Scene Objects/Scene Exit Trigger", false, 50)] private static void Exit(MenuCommand c) => Apply(c, SetupKind.SceneExit);

        [MenuItem(Root + "Interactions/Basic", true)]
        [MenuItem(Root + "Interactions/Flavor Text", true)]
        [MenuItem(Root + "Interactions/Conditional Message", true)]
        [MenuItem(Root + "Interactions/Dialogue", true)]
        [MenuItem(Root + "Interactions/Readable", true)]
        [MenuItem(Root + "Interactions/Animated", true)]
        [MenuItem(Root + "Interactions/Audio", true)]
        [MenuItem(Root + "Interactions/Hold", true)]
        [MenuItem(Root + "Interactions/Timed Progress", true)]
        [MenuItem(Root + "NPCs/Stationary", true)]
        [MenuItem(Root + "NPCs/Dialogue with Resolver", true)]
        [MenuItem(Root + "NPCs/Follow", true)]
        [MenuItem(Root + "NPCs/Wander", true)]
        [MenuItem(Root + "NPCs/Waypoint Route", true)]
        [MenuItem(Root + "Scene Objects/Scene Exit Trigger", true)]
        private static bool Validate(MenuCommand command) => !EditorApplication.isPlayingOrWillChangePlaymode && Targets(command).Any();

        private static GameObject[] Targets(MenuCommand command)
        {
            var context = command.context as GameObject;
            var targets = context != null && !Selection.gameObjects.Contains(context)
                ? new[] { context } : Selection.gameObjects;
            return targets.Where(item => item != null && !EditorUtility.IsPersistent(item) && item.scene.IsValid()).Distinct().ToArray();
        }

        private static void Apply(MenuCommand command, SetupKind kind)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Quiet Static: " + kind);
            try
            {
                foreach (GameObject target in Targets(command)) Attach(target, kind);
            }
            finally { Undo.CollapseUndoOperations(group); }
            Debug.Log($"Quiet Static {kind} setup complete. Configure content and any unassigned references in the Inspector.");
        }

        /// <summary>
        /// Adds missing components and local wiring without replacing authored references or events.
        /// Returns false for an incompatible interaction or movement composition.
        /// </summary>
        public static bool Attach(GameObject target, SetupKind kind)
        {
            if (target == null || EditorUtility.IsPersistent(target) || !target.scene.IsValid() || EditorApplication.isPlayingOrWillChangePlaymode)
                return false;
            if (!Enum.IsDefined(typeof(SetupKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            bool isInteraction = kind <= SetupKind.TimedProgress || kind == SetupKind.DialogueNPC;
            Type interactionType = kind == SetupKind.Hold ? typeof(HoldInteractable) :
                kind == SetupKind.TimedProgress ? typeof(ActivatedProgressInteractable) : typeof(Interactable);
            if (isInteraction && new[] { typeof(Interactable), typeof(HoldInteractable), typeof(ActivatedProgressInteractable) }
                .Any(type => type != interactionType && target.GetComponent(type) != null))
            {
                Debug.LogWarning($"Skipped {target.name}: it already has a different interaction type.", target);
                return false;
            }
            Type movement = kind == SetupKind.FollowNPC ? typeof(NPCFollowBehaviour) : kind == SetupKind.WanderNPC ? typeof(NPCWanderBehaviour) :
                kind == SetupKind.WaypointNPC ? typeof(NPCWaypointRouteBehaviour) : null;
            if (movement != null && new[] { typeof(NPCFollowBehaviour), typeof(NPCWanderBehaviour), typeof(NPCWaypointRouteBehaviour) }
                .Any(type => type != movement && target.GetComponent(type) != null))
            {
                Debug.LogWarning($"Skipped {target.name}: configure NPC modes before combining movement behaviours.", target);
                return false;
            }

            Component interaction = null;
            if (isInteraction)
            {
                bool newInteraction = target.GetComponent(interactionType) == null;
                interaction = target.GetComponent(interactionType) ?? Undo.AddComponent(target, interactionType);
                if (target.GetComponentInChildren<InteractionHighlighter>(true) == null) Add<InteractionHighlighter>(target);
                if (!InteractionTargetColliderUtility.HasRaycastCollider(interaction)) AddCollider(target);
                int layer = LayerMask.NameToLayer("Interactables");
                if (layer >= 0 && newInteraction)
                {
                    Undo.RecordObject(target, "Set Interaction Layer");
                    target.layer = layer;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                }
            }
            switch (kind)
            {
                case SetupKind.FlavorText:
                    // Reuse the clutter setup used by the dedicated authoring tool.
                    ClutterInteractableWindow.SetUp(target);
                    break;
                case SetupKind.ConditionalMessage:
                    var message = Add<ConditionalInteractionMessage>(target);
                    AssignUnique<InteractionUIChannel>(message, "channel");
                    Wire(interaction, "onInteractionSucceeded", message, "ShowMessage");
                    break;
                case SetupKind.Dialogue:
                    Wire(interaction, "onInteractionSucceeded", Add<DialogueEventPlayer>(target), "StartDialogue");
                    break;
                case SetupKind.Readable:
                    AssignUnique<InteractionUIChannel>(Add<ReadableInteractionTrigger>(target), "channel");
                    break;
                case SetupKind.Animated:
                    var unlock = Add<InteractableUnlock>(target);
                    Assign(unlock, "animator", target.GetComponent<Animator>());
                    Wire(interaction, "onInteractionSucceeded", unlock, "UnlockInteraction");
                    break;
                case SetupKind.Audio:
                    bool hasAudio = target.GetComponent<AudioSource>() != null;
                    var audio = Add<AudioSource>(target);
                    if (!hasAudio) { Undo.RecordObject(audio, "Configure Interaction Audio"); audio.playOnAwake = false; audio.spatialBlend = 1f; }
                    Wire(interaction, "onInteractionSucceeded", audio, "Play");
                    break;
                case SetupKind.StationaryNPC:
                case SetupKind.DialogueNPC:
                case SetupKind.FollowNPC:
                case SetupKind.WanderNPC:
                case SetupKind.WaypointNPC:
                    ConfigureNPC(target, kind, movement);
                    break;
                case SetupKind.SceneExit:
                    var exit = Add<SceneTransitionTrigger>(target);
                    AssignUnique<SceneFlowRequestChannel>(exit, "requestChannel");
                    AssignUnique<SceneFlowMap>(exit, "sceneFlowMap");
                    if (!target.GetComponents<Collider>().Any(collider => collider.isTrigger)) AddCollider(target);
                    break;
            }
            return true;
        }

        private static void ConfigureNPC(GameObject target, SetupKind kind, Type movement)
        {
            Add<NPCController>(target);
            var look = Add<NPCLookAtBehaviour>(target);
            Animator animator = target.GetComponentInChildren<Animator>(true);
            Assign(look, "animator", animator);
            if (movement != null)
            {
                var motor = Add<NPCNavMeshMotor>(target);
                Assign(motor, "agent", target.GetComponent<NavMeshAgent>());
                Component behaviour = target.GetComponent(movement) ?? Undo.AddComponent(target, movement);
                Assign(behaviour, "motor", motor);
                Assign(look, "motor", motor);
                if (animator != null)
                {
                    var driver = Add<NPCAnimatorDriver>(target);
                    Assign(driver, "animator", animator);
                    Assign(driver, "motor", motor);
                }
            }
            var dialogue = target.GetComponent<NPCDialogueBehaviour>();
            if (kind == SetupKind.DialogueNPC)
            {
                dialogue = Add<NPCDialogueBehaviour>(target);
                Assign(dialogue, "dialogueEventPlayer", Add<DialogueEventPlayer>(target));
                // Preserve a fixed conversation on existing NPCs; opt new NPCs into resolver authoring.
                if (dialogue.DialogueTree == null) Assign(dialogue, "dialogueResolver", Add<DialogueTreeResolver>(target));
                Wire(target.GetComponent<Interactable>(), "onInteractionSucceeded", dialogue, "StartDialogue");
            }
            if (dialogue != null)
            {
                Assign(dialogue, "lookAtBehaviour", look);
                Assign(dialogue, "motor", target.GetComponent<NPCNavMeshMotor>());
                using var data = new SerializedObject(dialogue);
                var paused = data.FindProperty("pauseDuringDialogue");
                if (paused.arraySize == 0)
                {
                    var behaviours = target.GetComponents<NPCBehaviour>().Where(behaviour =>
                        behaviour is NPCFollowBehaviour || behaviour is NPCWanderBehaviour || behaviour is NPCWaypointRouteBehaviour).ToArray();
                    paused.arraySize = behaviours.Length;
                    for (int index = 0; index < behaviours.Length; index++) paused.GetArrayElementAtIndex(index).objectReferenceValue = behaviours[index];
                    data.ApplyModifiedProperties();
                }
            }
        }

        private static T Add<T>(GameObject target) where T : Component
        {
            // Native components may return Unity's missing-component wrapper in the Editor.
            // Unity's equality operator recognises it; C# null-coalescing does not.
            T existing = target.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(target);
        }

        private static void Assign(Component owner, string field, UnityEngine.Object value)
        {
            if (value == null) return;
            using var data = new SerializedObject(owner);
            var property = data.FindProperty(field);
            if (property != null && property.objectReferenceValue == null)
            {
                property.objectReferenceValue = value;
                data.ApplyModifiedProperties();
            }
        }

        private static void AssignUnique<T>(Component owner, string field) where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids.Length == 1) Assign(owner, field, AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0])));
        }

        private static void Wire(Component owner, string field, UnityEngine.Object receiver, string method)
        {
            using var data = new SerializedObject(owner);
            var calls = data.FindProperty(field).FindPropertyRelative("m_PersistentCalls.m_Calls");
            for (int index = 0; index < calls.arraySize; index++)
            {
                var existing = calls.GetArrayElementAtIndex(index);
                if (existing.FindPropertyRelative("m_Target").objectReferenceValue == receiver &&
                    existing.FindPropertyRelative("m_MethodName").stringValue == method) return;
            }
            int next = calls.arraySize++;
            var call = calls.GetArrayElementAtIndex(next);
            call.FindPropertyRelative("m_Target").objectReferenceValue = receiver;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = receiver.GetType().AssemblyQualifiedName;
            call.FindPropertyRelative("m_MethodName").stringValue = method;
            call.FindPropertyRelative("m_Mode").intValue = (int)UnityEngine.Events.PersistentListenerMode.Void;
            call.FindPropertyRelative("m_CallState").intValue = (int)UnityEngine.Events.UnityEventCallState.RuntimeOnly;
            var arguments = call.FindPropertyRelative("m_Arguments");
            arguments.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = null;
            arguments.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = typeof(UnityEngine.Object).AssemblyQualifiedName;
            arguments.FindPropertyRelative("m_IntArgument").intValue = 0;
            arguments.FindPropertyRelative("m_FloatArgument").floatValue = 0;
            arguments.FindPropertyRelative("m_StringArgument").stringValue = string.Empty;
            arguments.FindPropertyRelative("m_BoolArgument").boolValue = false;
            data.ApplyModifiedProperties();
        }

        private static void AddCollider(GameObject target)
        {
            var collider = Undo.AddComponent<BoxCollider>(target);
            collider.isTrigger = true;
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = new(target.transform.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (Renderer renderer in renderers)
            {
                Bounds world = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                    bounds.Encapsulate(target.transform.InverseTransformPoint(world.center + Vector3.Scale(world.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))));
            }
            collider.center = bounds.center;
            collider.size = Vector3.Max(bounds.size, Vector3.one * .01f);
        }
    }
}
