using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>Shows configured readable content when its item interaction succeeds.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Interactable))]
    [AddComponentMenu("Quiet Static Toolkit/Interactions/Readable Interaction Trigger")]
    public sealed class ReadableInteractionTrigger : MonoBehaviour
    {
        [Tooltip("Cross-scene UI channel received by the persistent readable UI scene.")]
        [SerializeField] private InteractionUIChannel channel;
        [Tooltip("Letter, note, or other content shown after this item is interacted with.")]
        [SerializeField] private ReadableContentDefinition content;
        [Header("Events")]
        [Tooltip("Invoked after this readable is displayed by the persistent overlay.")]
        [SerializeField] private UnityEvent onOpened;
        [Tooltip("Invoked after this readable closes. Use this for thoughts, dialogue, flags, or other follow-up behavior.")]
        [SerializeField] private UnityEvent onClosed;
        private Interactable target;

        private void Awake() => target = GetComponent<Interactable>();
        private void OnEnable()
        {
            Interactable.OnInteractionSucceeded += HandleInteraction;
            if (channel != null)
            {
                channel.ReadableOpened += HandleReadableOpened;
                channel.ReadableClosed += HandleReadableClosed;
            }
        }

        private void OnDisable()
        {
            Interactable.OnInteractionSucceeded -= HandleInteraction;
            if (channel != null)
            {
                channel.ReadableOpened -= HandleReadableOpened;
                channel.ReadableClosed -= HandleReadableClosed;
            }
        }

        /// <summary>Shows this trigger's content directly, including from a UnityEvent.</summary>
        public void Show()
        {
            if (channel != null && content != null) channel.ShowReadable(content, this);
        }

        private void HandleInteraction(Interactable interacted, Interactor interactor)
        {
            if (interacted == target) Show();
        }

        private void HandleReadableOpened(
            ReadableContentDefinition definition,
            Object source)
        {
            if (source == this && definition == content) onOpened?.Invoke();
        }

        private void HandleReadableClosed(
            ReadableContentDefinition definition,
            Object source)
        {
            if (source == this && definition == content) onClosed?.Invoke();
        }
    }
}
