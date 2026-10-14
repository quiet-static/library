using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>Slides resolution-independent black bars into view for cinematic scenes.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Quiet Static Toolkit/Cinematics/Cinematic Bars Overlay")]
    public sealed class CinematicBarsOverlay : MonoBehaviour
    {
        [Tooltip("Full-width top bar on the overlay canvas.")]
        [SerializeField] private RectTransform topBar;
        [Tooltip("Full-width bottom bar on the overlay canvas.")]
        [SerializeField] private RectTransform bottomBar;
        [Tooltip("Height of each bar as a fraction of the screen height.")]
        [Range(0f, 0.45f)] [SerializeField] private float barHeight = 0.12f;
        [Tooltip("Seconds to fully show or hide the bars. Uses unscaled time so paused cutscenes still animate.")]
        [Min(0f)] [SerializeField] private float transitionDuration = 0.3f;
        [Tooltip("Show immediately on enable when no playback source is assigned. Useful for cinematic-only scenes.")]
        [SerializeField] private bool showOnEnable = true;
        [Tooltip("Optional sequence to follow automatically, including when stopped or disabled. Overrides manual visibility.")]
        [SerializeField] private CutsceneSequenceRunner sequenceRunner;
        [Tooltip("Optional cinematic player to follow automatically. Visible while either assigned source is playing.")]
        [SerializeField] private CinematicScenePlayer scenePlayer;

        private float visibility;
        private bool targetVisible;

        /// <summary>Current reveal amount, from hidden (0) to fully shown (1).</summary>
        public float Visibility => visibility;

        private bool HasPlaybackSource => sequenceRunner != null || scenePlayer != null;
        private bool PlaybackActive =>
            (sequenceRunner != null && sequenceRunner.isActiveAndEnabled && sequenceRunner.IsRunning) ||
            (scenePlayer != null && scenePlayer.isActiveAndEnabled && scenePlayer.IsRunning);

        private void OnEnable() => SetVisibleInstant(HasPlaybackSource ? PlaybackActive : showOnEnable);

        private void Update()
        {
            if (HasPlaybackSource) targetVisible = PlaybackActive;
            visibility = transitionDuration <= 0f
                ? (targetVisible ? 1f : 0f)
                : Mathf.MoveTowards(visibility, targetVisible ? 1f : 0f, Time.unscaledDeltaTime / transitionDuration);
            ApplyLayout();
        }

        /// <summary>Animates the bars into view. Can be called by a UnityEvent.</summary>
        public void Show() => targetVisible = true;

        /// <summary>Animates the bars out of view. Keep the overlay object enabled during the animation.</summary>
        public void Hide() => targetVisible = false;

        /// <summary>Shows the bars without animation.</summary>
        public void ShowInstant() => SetVisibleInstant(true);

        /// <summary>Hides the bars without animation.</summary>
        public void HideInstant() => SetVisibleInstant(false);

        private void SetVisibleInstant(bool show)
        {
            targetVisible = show;
            visibility = show ? 1f : 0f;
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            float height = Mathf.Clamp(barHeight, 0f, 0.45f) * visibility;
            SetBar(topBar, 1f - height, 1f, height > 0f);
            SetBar(bottomBar, 0f, height, height > 0f);
        }

        private static void SetBar(RectTransform bar, float minY, float maxY, bool visible)
        {
            if (bar == null) return;
            bar.anchorMin = new Vector2(0f, minY);
            bar.anchorMax = new Vector2(1f, maxY);
            bar.offsetMin = bar.offsetMax = Vector2.zero;
            if (bar.gameObject.activeSelf != visible) bar.gameObject.SetActive(visible);
        }
    }
}
