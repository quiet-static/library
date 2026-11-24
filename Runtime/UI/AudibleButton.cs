using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.UI
{
    /// <summary>A button that requests UI feedback immediately before user activation.</summary>
    [AddComponentMenu("Quiet Static Toolkit/UI/Audible Button")]
    public sealed class AudibleButton : Button
    {
        internal static event Action Activated;

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                PlayFeedback();

            base.OnPointerClick(eventData);
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            PlayFeedback();
            base.OnSubmit(eventData);
        }

        private void PlayFeedback()
        {
            if (IsActive() && IsInteractable())
                Activated?.Invoke();
        }
    }
}
