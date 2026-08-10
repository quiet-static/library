using System.Collections;
using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Requests a screen fade to clear shortly after its scene starts.
    /// </summary>
    /// <remarks>
    /// Place this component in a destination scene that may be loaded while a
    /// persistent <see cref="ScreenFader"/> is black. The fader can be assigned
    /// explicitly or discovered at runtime.
    /// </remarks>
    public class FadeToClearOnStart : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Screen fader to clear. If left empty, the component finds an active fader at runtime.")]
        [SerializeField] private ScreenFader screenFader;

        [Header("Timing")]
        [Tooltip("Wait one frame before resolving and starting the fade. This allows additive scene services to initialize.")]
        [SerializeField] private bool waitOneFrame = true;

        [Tooltip("Additional delay, in unscaled seconds, before the fade begins.")]
        [Min(0f)]
        [SerializeField] private float delayBeforeFade = 0.1f;

        private IEnumerator Start()
        {
            if (waitOneFrame)
            {
                yield return null;
            }

            if (delayBeforeFade > 0f)
            {
                yield return new WaitForSecondsRealtime(delayBeforeFade);
            }

            if (screenFader == null)
            {
                screenFader = FindAnyObjectByType<ScreenFader>();
            }

            if (screenFader == null)
            {
                GameLogger.Warning(nameof(FadeToClearOnStart), this,
                    $"Could not find a {nameof(ScreenFader)}.");
                yield break;
            }

            screenFader.FadeToClear();
        }
    }
}
