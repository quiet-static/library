using System;
using System.Collections;
using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>Target state requested through a <see cref="ScreenFadeChannel"/>.</summary>
    public enum ScreenFadeTarget
    {
        Clear,
        Black
    }

    /// <summary>One completion-aware cross-scene fade request.</summary>
    public sealed class ScreenFadeRequest
    {
        internal ScreenFadeRequest(ScreenFadeTarget target, float duration)
        {
            Target = target;
            Duration = Mathf.Max(0f, duration);
        }

        /// <summary>Requested final screen state.</summary>
        public ScreenFadeTarget Target { get; }

        /// <summary>Requested fade duration in unscaled seconds.</summary>
        public float Duration { get; }

        /// <summary>Gets whether the receiving handler completed the fade.</summary>
        public bool IsComplete { get; private set; }

        /// <summary>Gets whether a newer request or disabled handler canceled this fade.</summary>
        public bool WasCancelled { get; private set; }

        /// <summary>Marks this request complete from the runtime fade handler.</summary>
        internal void Complete() => IsComplete = true;

        /// <summary>Releases callers waiting on a fade that can no longer complete.</summary>
        internal void Cancel()
        {
            WasCancelled = true;
            IsComplete = true;
        }
    }

    /// <summary>Routes completion-aware fade requests between separately loaded scenes.</summary>
    [CreateAssetMenu(
        fileName = "ScreenFadeChannel",
        menuName = "Quiet Static Toolkit/Cinematics/Screen Fade Channel")]
    public sealed class ScreenFadeChannel : ScriptableObject
    {
        private event Action<ScreenFadeRequest> HandlerRequested;

        /// <summary>
        /// Raised for observation after a handler accepts a fade request. Observers cannot
        /// complete the request, and one failing observer does not interrupt the fade.
        /// </summary>
        public event Action<ScreenFadeRequest> FadeRequested;

        /// <summary>Gets whether an enabled handler is subscribed.</summary>
        public bool HasReceiver => HandlerRequested != null;

        /// <summary>Registers one completion-capable screen-fade handler.</summary>
        internal void RegisterHandler(Action<ScreenFadeRequest> handler)
        {
            if (handler == null)
            {
                return;
            }

            HandlerRequested -= handler;
            HandlerRequested += handler;
        }

        /// <summary>Unregisters a completion-capable screen-fade handler.</summary>
        internal void UnregisterHandler(Action<ScreenFadeRequest> handler)
        {
            if (handler != null)
            {
                HandlerRequested -= handler;
            }
        }

        /// <summary>Requests a fade and waits until its handler reports completion.</summary>
        public IEnumerator FadeRoutine(ScreenFadeTarget target, float duration)
        {
            ScreenFadeRequest request = new(target, duration);
            Action<ScreenFadeRequest> handlers = HandlerRequested;
            if (handlers == null)
            {
                GameLogger.Warning(
                    nameof(ScreenFadeChannel),
                    this,
                    $"{nameof(ScreenFadeChannel)} has no active handler.");
                yield break;
            }

            handlers.Invoke(request);
            NotifyObservers(request);
            yield return new WaitUntil(() => request.IsComplete);
        }

        private void NotifyObservers(ScreenFadeRequest request)
        {
            Action<ScreenFadeRequest> observers = FadeRequested;
            if (observers == null)
            {
                return;
            }

            foreach (Action<ScreenFadeRequest> observer in observers.GetInvocationList())
            {
                try
                {
                    observer.Invoke(request);
                }
                catch (Exception exception)
                {
                    GameLogger.Exception(
                        exception,
                        $"notifying a {nameof(ScreenFadeChannel)} observer",
                        this);
                }
            }
        }
    }
}
