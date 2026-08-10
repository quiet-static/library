using System.Collections;
using TMPro;
using UnityEngine;

namespace QuietStatic.Toolkit.Settings
{
    /// <summary>UnityEvent-friendly presenter for meaningful non-dialogue sound captions.</summary>
    [AddComponentMenu("Quiet Static Toolkit/UI/Closed Caption Presenter")]
    public sealed class ClosedCaptionPresenter : MonoBehaviour
    {
        [Tooltip("Text element used for meaningful non-dialogue sound descriptions.")]
        [SerializeField] private TMP_Text captionText;
        [Tooltip("Seconds a caption remains visible. Uses real time so pausing does not freeze captions.")]
        [Min(0f)] [SerializeField] private float displayDuration = 2.5f;
        private Coroutine hideRoutine;

        private void Awake() => Hide();

        public void ShowCaption(string caption)
        {
            if (SettingsManager.Instance != null && !SettingsManager.Instance.ClosedCaptionsEnabled) return;
            if (captionText == null || string.IsNullOrWhiteSpace(caption)) return;
            captionText.text = caption;
            captionText.gameObject.SetActive(true);
            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideAfterDelay());
        }

        public void Hide()
        {
            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = null;
            if (captionText != null) captionText.gameObject.SetActive(false);
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSecondsRealtime(displayDuration);
            hideRoutine = null;
            if (captionText != null) captionText.gameObject.SetActive(false);
        }
    }
}
