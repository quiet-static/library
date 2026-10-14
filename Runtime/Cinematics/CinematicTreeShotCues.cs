using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>Maps stable cinematic text beat IDs to scene-authored shots and reactions.</summary>
    [DisallowMultipleComponent]
    public sealed class CinematicTreeShotCues : MonoBehaviour
    {
        [Serializable] public sealed class Cue
        {
            [Tooltip("Stable text beat selected from the assigned player's tree.")]
            public string beatId;
            public CinematicCutsceneCameraDirector cameraDirector;
            [CinematicShotId(nameof(cameraDirector))] public string cameraShotId;
            public UnityEvent onBeatEntered;
        }
        [SerializeField] private CinematicTreePlayer player;
        [SerializeField] private List<Cue> cues = new();
        private void OnEnable()
        {
            if (player == null) return;
            player.BeatStarted += Apply;
            if (player.IsRunning) Apply(player.CurrentBeat);
        }
        private void OnDisable() { if (player != null) player.BeatStarted -= Apply; }
        private void Apply(CinematicTree.Beat beat)
        {
            if (beat == null) return;
            foreach (var cue in cues)
            {
                if (cue == null || !string.Equals(beat.id, cue.beatId, StringComparison.Ordinal)) continue;
                if (cue.cameraDirector != null && !string.IsNullOrWhiteSpace(cue.cameraShotId)) cue.cameraDirector.CutToShot(cue.cameraShotId);
                cue.onBeatEntered?.Invoke();
            }
        }
    }
}
