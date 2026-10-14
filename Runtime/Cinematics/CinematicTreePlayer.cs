using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>Plays cinematic text strictly in array order and exposes stable beat events.</summary>
    [DisallowMultipleComponent]
    public sealed class CinematicTreePlayer : MonoBehaviour, ICinematicWaitSource
    {
        [Serializable] public sealed class BeatIdEvent : UnityEvent<string> { }
        [SerializeField, Tooltip("Ordered cinematic text asset.")] private CinematicTree tree;
        [SerializeField, Tooltip("Overlay receiving each text beat.")] private CinematicTextOverlay overlay;
        [SerializeField] private bool playOnStart;
        [SerializeField, Tooltip("Advance automatically after the reveal and each beat's hold time.")] private bool autoAdvance = true;
        [SerializeField, Tooltip("Optional input action, owned and enabled by the project's input system.")] private InputActionReference advanceAction;
        [SerializeField, Tooltip("Use Space, Enter or left click when no input action is assigned.")] private bool allowDefaultInput = true;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private UnityEvent onStarted;
        [SerializeField] private BeatIdEvent onBeatStarted;
        [SerializeField] private BeatIdEvent onBeatFinished;
        [SerializeField] private UnityEvent onFinished;
        [SerializeField, Tooltip("Runs on natural completion and cancellation.")] private UnityEvent onStopped;
        public event Action<CinematicTree.Beat> BeatStarted;
        public event Action<CinematicTree.Beat> BeatFinished;
        public bool IsRunning { get; private set; }
        public int CurrentBeatIndex { get; private set; } = -1;
        public CinematicTree Tree => tree;
        public CinematicTree.Beat CurrentBeat => IsRunning && tree != null && CurrentBeatIndex >= 0 && CurrentBeatIndex < tree.Beats.Count ? tree.Beats[CurrentBeatIndex] : null;
        private float holdRemaining;
        private int session;
        private bool advancing;
        private void Start() { if (playOnStart) Play(); }
        private void OnDisable() => Stop();

        private void Update()
        {
            if (!IsRunning) return;
            if (overlay == null || !overlay.isActiveAndEnabled || CurrentBeat == null) { Stop(); return; }
            bool advance = advanceAction != null ? advanceAction.action.WasPressedThisFrame() : allowDefaultInput &&
                ((Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)) ||
                 (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame));
            if (advance) { Advance(); return; }
            if (!autoAdvance || overlay.IsRevealing) return;
            holdRemaining -= useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (holdRemaining <= 0) NextBeat();
        }

        /// <summary>Changes the asset when idle, preserving the active playback session.</summary>
        public void SetTree(CinematicTree value) { if (!IsRunning) tree = value; }

        /// <summary>Starts at the first beat, suitable for UnityEvents and cinematic wait-source bindings.</summary>
        public void Play()
        {
            if (IsRunning || !isActiveAndEnabled || tree == null) return;
            if (!tree.TryValidate(out string error)) { Debug.LogWarning(error, this); return; }
            if (overlay == null || !overlay.isActiveAndEnabled || overlay.TextLabel == null)
            { Debug.LogWarning("Assign an active cinematic text overlay before playback.", this); return; }
            session++;
            IsRunning = true;
            CurrentBeatIndex = -1;
            int token = session;
            onStarted?.Invoke();
            if (IsRunning && token == session) EnterBeat(0);
        }

        /// <summary>First advance completes typing; a subsequent advance moves to the next beat.</summary>
        public void Advance()
        {
            if (!IsRunning) return;
            if (overlay != null && overlay.IsRevealing) overlay.RevealAll();
            else NextBeat();
        }

        private void EnterBeat(int index)
        {
            session++;
            CurrentBeatIndex = index;
            var beat = CurrentBeat;
            if (beat == null || !overlay.ShowText(beat.text, beat.charactersPerSecond)) { Stop(); return; }
            holdRemaining = beat.holdSeconds;
            int token = session;
            BeatStarted?.Invoke(beat);
            if (IsRunning && token == session) onBeatStarted?.Invoke(beat.id);
        }

        private void NextBeat()
        {
            if (advancing) return;
            advancing = true;
            try
            {
            var beat = CurrentBeat;
            if (beat == null) { Stop(); return; }
            int token = session;
            BeatFinished?.Invoke(beat);
            if (!IsRunning || token != session) return;
            onBeatFinished?.Invoke(beat.id);
            if (!IsRunning || token != session) return;
            int next = CurrentBeatIndex + 1;
            if (next < tree.Beats.Count) EnterBeat(next);
            else { Stop(); if (token + 1 == session) onFinished?.Invoke(); }
            }
            finally { advancing = false; }
        }

        /// <summary>Cancels playback and hides text without invoking natural completion.</summary>
        public void Stop()
        {
            if (!IsRunning) return;
            session++;
            IsRunning = false;
            CurrentBeatIndex = -1;
            if (overlay != null) overlay.Clear();
            onStopped?.Invoke();
        }
    }
}
