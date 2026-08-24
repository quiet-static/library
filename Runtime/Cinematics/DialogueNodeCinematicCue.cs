using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Dialogue;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>Applies scene-authored cinematic presentation when dialogue enters a named node.</summary>
    /// <remarks>
    /// Dialogue assets remain reusable story data; this scene component maps their stable node IDs
    /// to camera shots, character actions, and local UnityEvents.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Quiet Static Toolkit/Cinematics/Dialogue Node Cinematic Cues")]
    public sealed class DialogueNodeCinematicCue : MonoBehaviour
    {
        /// <summary>Presentation changes applied when a matching dialogue node is entered.</summary>
        [Serializable]
        public sealed class Cue
        {
            [Tooltip("Stable DialogueTree node ID that activates this cue. Matching is case-sensitive.")]
            public string nodeId;

            [Tooltip("Optional camera director that owns the selectable shot.")]
            public CinematicCutsceneCameraDirector cameraDirector;

            [Tooltip("Stable camera shot selected from the assigned director. Leave empty to keep the current shot.")]
            [CinematicShotId(nameof(cameraDirector))]
            public string cameraShotId;

            [Tooltip("Optional preconfigured character actions to run as this node appears.")]
            public CutsceneCharacterStepTrigger characterActions;

            [Tooltip("Additional scene-local reactions, such as audio, lighting, props, or particles.")]
            public UnityEvent onNodeEntered;
        }

        [Header("Dialogue Source")]
        [Tooltip("Only node changes from this Dialogue Runner activate cues.")]
        [SerializeField] private DialogueRunner dialogueRunner;

        [Header("Node Cues")]
        [Tooltip("Mappings from stable dialogue node IDs to scene presentation changes.")]
        [SerializeField] private List<Cue> cues = new();

        private void Reset() => dialogueRunner = GetComponentInChildren<DialogueRunner>(true);

        private void OnEnable()
        {
            if (dialogueRunner != null)
            {
                dialogueRunner.NodeChanged += HandleNodeChanged;
            }
        }

        private void OnDisable()
        {
            if (dialogueRunner != null)
            {
                dialogueRunner.NodeChanged -= HandleNodeChanged;
            }
        }

        private void HandleNodeChanged(DialogueRunner source, DialogueTree.Node node)
        {
            if (source != dialogueRunner || node == null || string.IsNullOrEmpty(node.id)) return;

            foreach (Cue cue in cues)
            {
                if (cue == null || !string.Equals(cue.nodeId, node.id, StringComparison.Ordinal)) continue;
                if (cue.cameraDirector != null &&
                    !string.IsNullOrWhiteSpace(cue.cameraShotId))
                {
                    cue.cameraDirector.CutToShot(cue.cameraShotId);
                }
                cue.characterActions?.Run();
                cue.onNodeEntered?.Invoke();
            }
        }
    }
}
