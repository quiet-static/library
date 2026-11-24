using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SetupKind = QuietStatic.Toolkit.Editor.Setup.GameObjectSetupMenu.SetupKind;

namespace QuietStatic.Toolkit.Editor.Setup
{
    /// <summary>Previews a hierarchy setup and the Inspector fields it needs before applying it.</summary>
    internal sealed class GameObjectSetupWindow : EditorWindow
    {
        private static readonly (SetupKind kind, string label, string purpose, string next)[] Recipes =
        {
            (SetupKind.BasicInteraction, "Interactions/Basic", "Run an event when the player interacts once.",
                "Set the Display Name prompt and wire On Interaction Succeeded to the desired action."),
            (SetupKind.FlavorText, "Interactions/Flavor Text", "Show a short observation when the player examines a prop.",
                "Enter the prompt and Flavor Text, then check the Interaction UI Channel."),
            (SetupKind.ConditionalMessage, "Interactions/Conditional Message", "Show different text depending on flags.",
                "Choose the Interaction UI Channel, order rules from most to least specific, and set fallback text."),
            (SetupKind.Dialogue, "Interactions/Dialogue", "Start a conversation from a scene object.",
                "Assign a Dialogue Tree in Dialogue Event Player. Set a speaker or focus target as needed."),
            (SetupKind.Readable, "Interactions/Readable", "Open a note or similar item in the readable overlay.",
                "Assign a Readable Content Definition and the Interaction UI Channel used by the overlay."),
            (SetupKind.Animated, "Interactions/Animated", "Trigger an Animator when the player interacts.",
                "Assign the Animator and its ON trigger. Set the OFF trigger and starting state for a binary toggle."),
            (SetupKind.Audio, "Interactions/Audio", "Play a spatial sound on successful interaction.",
                "Assign a clip to Audio Source and adjust volume and spatial range."),
            (SetupKind.Hold, "Interactions/Hold", "Require the player to keep holding the interaction input.",
                "Set the prompt and hold duration, then wire the completion event."),
            (SetupKind.TimedProgress, "Interactions/Timed Progress", "Start a timer that continues after the player lets go.",
                "Set the duration, assign a world-space progress bar if needed, and wire the completion event."),
            (SetupKind.StationaryNPC, "NPCs/Stationary", "Keep an NPC in place with optional look-at behavior.",
                "Assign a target to the NPC Controller for look-at and check the Animator reference."),
            (SetupKind.DialogueNPC, "NPCs/Dialogue with Resolver", "Talk to an NPC whose conversation can change with story flags and player status.",
                "Set the interaction prompt, then configure ordered rules and an optional fallback tree in Dialogue Tree Resolver."),
            (SetupKind.FollowNPC, "NPCs/Follow", "Move an NPC toward its assigned target.",
                "Assign the NPC Controller target, set the follow distance, and place the NPC on a baked NavMesh."),
            (SetupKind.WanderNPC, "NPCs/Wander", "Let an NPC roam within a radius or visit patrol points.",
                "Choose a wander mode and set the radius or patrol points. Place the NPC on a baked NavMesh."),
            (SetupKind.WaypointNPC, "NPCs/Waypoint Route", "Move an NPC along an authored waypoint route.",
                "Assign the Route, choose a traversal mode, and place the NPC on a baked NavMesh."),
            (SetupKind.SceneExit, "Scene Objects/Scene Exit Trigger", "Request a mapped transition when an eligible collider enters.",
                "Assign the Scene Flow Map, Connection ID, and Request Channel. Check the trigger bounds and Required Tag."),
            (SetupKind.DialogueTrigger, "Scene Objects/Dialogue Trigger", "Start dialogue when the player enters a dedicated volume.",
                "Check Start On Trigger Enter, the trigger bounds, and Required Tag. Assign a Dialogue Tree. For contextual selection, add and assign a Dialogue Tree Resolver. Add Dialogue NPC Reaction for optional NPC effects.")
        };

        private static readonly string[] RecipeLabels = Recipes.Select(recipe => recipe.label).ToArray();

        [SerializeField] private GameObject[] targets = Array.Empty<GameObject>();
        [SerializeField] private int selectedRecipe;
        private Vector2 scroll;
        private string result;

        internal static void Open(GameObject[] targets)
        {
            var window = GetWindow<GameObjectSetupWindow>(true, "Quiet Static Setup");
            window.minSize = new Vector2(395f, 315f);
            window.targets = targets;
            window.result = null;
            window.Repaint();
        }

        private void OnGUI()
        {
            GameObject[] available = targets.Where(target => target != null && target.scene.isLoaded).ToArray();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Attach Setup", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(available.Length == 1
                ? $"Target: {available[0].name}"
                : $"Selected objects: {available.Length}", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            selectedRecipe = EditorGUILayout.Popup("Setup", selectedRecipe, RecipeLabels);
            if (EditorGUI.EndChangeCheck()) result = null;
            var recipe = Recipes[selectedRecipe];
            EditorGUILayout.LabelField(recipe.purpose, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Configure in the Inspector", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(recipe.next, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Setup fills missing components and wiring. Review added colliders and save the scene or prefab after configuring the setup. Use Undo to revert the batch.", MessageType.Info);

            if (available.Length == 0)
                EditorGUILayout.HelpBox("Select scene objects in the Hierarchy and reopen Choose Setup.", MessageType.Warning);
            else if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("Exit Play Mode to apply a setup.", MessageType.Warning);
            if (!string.IsNullOrEmpty(result))
                EditorGUILayout.HelpBox(result, MessageType.Info);
            using (new EditorGUI.DisabledScope(available.Length == 0 || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Apply Setup"))
                {
                    int applied = GameObjectSetupMenu.ApplyToTargets(available, recipe.kind);
                    result = $"Applied to {applied} of {available.Length} object(s).";
                    if (applied < available.Length) result += " See the Console for skipped objects.";
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
