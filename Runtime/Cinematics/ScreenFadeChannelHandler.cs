using System;
using System.Collections;
using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>Connects a scene-local <see cref="ScreenFader"/> to a cross-scene channel.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScreenFader))]
    [AddComponentMenu("Quiet Static Toolkit/Cinematics/Screen Fade Channel Handler")]
    public sealed class ScreenFadeChannelHandler : MonoBehaviour
    {
        [Tooltip("Required channel shared with systems that request screen fades.")]
        [SerializeField] private ScreenFadeChannel channel;

        [Tooltip("Scene-local fader that performs requests. Auto-filled from this object.")]
        [SerializeField] private ScreenFader screenFader;

        private Coroutine activeRequestRoutine;
        private ScreenFadeRequest activeRequest;

        private void Reset() => screenFader = GetComponent<ScreenFader>();

        private void OnEnable()
        {
            if (screenFader == null)
            {
                screenFader = GetComponent<ScreenFader>();
            }

            if (channel != null)
            {
                channel.RegisterHandler(HandleFadeRequested);
            }
        }

        private void OnDisable()
        {
            if (channel != null)
            {
                channel.UnregisterHandler(HandleFadeRequested);
            }

            CancelActiveRequest(clearFader: true);
        }

        private void HandleFadeRequested(ScreenFadeRequest request)
        {
            CancelActiveRequest(clearFader: false);
            activeRequest = request;
            Coroutine startedRoutine = StartCoroutine(PerformFade(request));
            if (!request.IsComplete)
            {
                activeRequestRoutine = startedRoutine;
            }
        }

        private IEnumerator PerformFade(ScreenFadeRequest request)
        {
            if (screenFader != null)
            {
                screenFader.StopActiveFade();
                yield return request.Target == ScreenFadeTarget.Black
                    ? screenFader.FadeToBlackRoutine(request.Duration)
                    : screenFader.FadeToClearRoutine(request.Duration);
            }

            request.Complete();
            if (ReferenceEquals(activeRequest, request))
            {
                activeRequest = null;
                activeRequestRoutine = null;
            }
        }

        private void CancelActiveRequest(bool clearFader)
        {
            if (activeRequestRoutine != null)
            {
                StopCoroutine(activeRequestRoutine);
            }

            if (screenFader != null)
            {
                if (clearFader)
                {
                    screenFader.SetClearInstant();
                }
                else
                {
                    screenFader.StopActiveFade();
                }
            }

            activeRequestRoutine = null;
            activeRequest?.Cancel();
            activeRequest = null;
        }
    }
}
