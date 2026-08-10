using System;
using System.Collections;
using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Horror
{
    /// <summary>Applies music, entry SFX, and overlay effects for tension states.</summary>
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Quiet Static Toolkit/Horror/Tension Controller")]
    public sealed class HorrorTensionController : MonoBehaviour
    {
        [Serializable] public sealed class StringUnityEvent : UnityEvent<string> { }

        [Tooltip("Reusable state definition evaluated explicitly or against the active FlagManager.")]
        [SerializeField] private HorrorTensionDefinition definition;
        [Tooltip("Evaluate the definition once after persistent managers initialize.")]
        [SerializeField] private bool evaluateOnStart = true;
        [Tooltip("Reevaluate the highest-priority state whenever flags change.")]
        [SerializeField] private bool reactToFlagChanges = true;

        [Header("Effects")]
        [Tooltip("Two-dimensional source used for one-shot state-entry stingers.")]
        [SerializeField] private AudioSource entrySfxSource;
        [Tooltip("Fullscreen overlay alpha controlled by the selected state.")]
        [SerializeField] private CanvasGroup overlayCanvasGroup;
        [Tooltip("Optional overlay image that receives the selected state's color.")]
        [SerializeField] private Image overlayImage;

        [Header("Events")]
        [Tooltip("Invoked with the stable ID after a new tension state is applied.")]
        [SerializeField] private StringUnityEvent onStateEntered;
        [Tooltip("Invoked with the stable ID immediately before the previous state is left.")]
        [SerializeField] private StringUnityEvent onStateExited;

        private Coroutine overlayRoutine;
        public HorrorTensionDefinition.State CurrentState { get; private set; }
        public string CurrentStateId => CurrentState?.Id ?? string.Empty;
        public static event Action<HorrorTensionController, string, string> StateChanged;

        private void Awake()
        {
            entrySfxSource ??= GetComponent<AudioSource>();
            entrySfxSource.playOnAwake = false;
            entrySfxSource.loop = false;
            entrySfxSource.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            FlagManager.OnFlagsChanged += HandleFlagsChanged;
        }

        private void OnDisable()
        {
            FlagManager.OnFlagsChanged -= HandleFlagsChanged;
        }

        private IEnumerator Start()
        {
            yield return null;
            if (evaluateOnStart) ReevaluateFromFlags();
        }

        /// <summary>UnityEvent entry point that applies a state by stable ID.</summary>
        public void SetState(string stateId)
        {
            HorrorTensionDefinition.State state = definition?.FindState(stateId);
            if (state == null)
            {
                GameLogger.Warning(nameof(SetState), this,
                    $"No tension state named '{stateId}' exists.");
                return;
            }
            ApplyState(state);
        }

        /// <summary>UnityEvent entry point that selects the best current flag rule.</summary>
        public void ReevaluateFromFlags()
        {
            HorrorTensionDefinition.State selected = definition?.SelectState();
            if (selected != null) ApplyState(selected);
        }

        private void HandleFlagsChanged()
        {
            if (reactToFlagChanges) ReevaluateFromFlags();
        }

        private void ApplyState(HorrorTensionDefinition.State state)
        {
            if (state == CurrentState) return;
            string previous = CurrentStateId;
            if (CurrentState != null) onStateExited?.Invoke(previous);
            CurrentState = state;
            ApplyMusic(state);
            PlayEntrySfx(state);
            StartOverlayTransition(state);
            onStateEntered?.Invoke(state.Id);
            StateChanged?.Invoke(this, previous, state.Id);
        }

        private static void ApplyMusic(HorrorTensionDefinition.State state)
        {
            if (MusicManager.Instance == null) return;
            switch (state.MusicAction)
            {
                case TensionMusicAction.Play when state.Music != null:
                    if (state.FadeMusic) MusicManager.Instance.PlayMusicWithFade(state.Music);
                    else MusicManager.Instance.PlayMusic(state.Music);
                    break;
                case TensionMusicAction.Stop:
                    if (state.FadeMusic) MusicManager.Instance.StopMusicWithFade();
                    else MusicManager.Instance.StopMusic();
                    break;
            }
        }

        private void PlayEntrySfx(HorrorTensionDefinition.State state)
        {
            if (entrySfxSource == null || state.EntrySfx.Count == 0) return;
            AudioClip clip = state.EntrySfx[UnityEngine.Random.Range(0, state.EntrySfx.Count)];
            if (clip != null) entrySfxSource.PlayOneShot(clip, state.EntrySfxVolume);
        }

        private void StartOverlayTransition(HorrorTensionDefinition.State state)
        {
            if (overlayCanvasGroup == null) return;
            if (overlayRoutine != null) StopCoroutine(overlayRoutine);
            if (overlayImage != null) overlayImage.color = state.OverlayColor;
            overlayRoutine = StartCoroutine(FadeOverlay(
                state.OverlayAlpha, state.OverlayTransitionDuration));
        }

        private IEnumerator FadeOverlay(float target, float duration)
        {
            float start = overlayCanvasGroup.alpha;
            if (duration <= 0f) overlayCanvasGroup.alpha = target;
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    overlayCanvasGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                    yield return null;
                }
                overlayCanvasGroup.alpha = target;
            }
            overlayRoutine = null;
        }
    }
}
