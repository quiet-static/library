using System.Collections.Generic;
using QuietStatic.Toolkit.Input;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>Renders long-form interaction text over a translucent backdrop.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Quiet Static Toolkit/Interactions/Readable Overlay Handler")]
    public sealed class ReadableOverlayHandler : MonoBehaviour
    {
        [Header("Channel")]
        [SerializeField] private InteractionUIChannel channel;
        [Header("Overlay")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backdrop;
        [Range(0f, 1f)] [SerializeField] private float backdropAlpha = 0.72f;
        [Header("Text")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text closeLabelText;
        [SerializeField] private string defaultCloseLabel = "Close";
        [Header("Dismiss")]
        [SerializeField] private bool closeWithEscape = true;
        [Tooltip("Optional Input System action used to close the overlay. If none is assigned, the Escape key is used.")]
        [SerializeField] private InputActionReference closeAction;
        [Header("Modal Context")]
        [Tooltip("Input groups suppressed while readable content is visible.")]
        [SerializeField] private InputBlockGroups blockedInput = InputBlockGroups.Gameplay;
        [Tooltip("UI roots temporarily hidden while readable content is visible. Their previous active states are restored when it closes.")]
        [SerializeField] private GameObject[] hiddenWhileVisible;
        [Header("Events")]
        [SerializeField] private UnityEvent onOpened;
        [SerializeField] private UnityEvent onClosed;

        private readonly Dictionary<GameObject, bool> hiddenObjectStates = new();
        private InputBlockHandle inputBlockHandle;
        private ReadableContentDefinition activeDefinition;
        private Object activeSource;

        /// <summary>Gets whether readable content is currently visible.</summary>
        public bool IsVisible { get; private set; }

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (backdrop != null) backdrop.transform.SetAsFirstSibling();
            IsVisible = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void OnEnable() { if (channel != null) channel.CommandRequested += HandleCommand; }
        private void OnDisable()
        {
            if (channel != null) channel.CommandRequested -= HandleCommand;
            if (IsVisible) SetVisible(false);
        }
        private void Update()
        {
            if (!IsVisible || !closeWithEscape) return;

            InputAction action = closeAction != null ? closeAction.action : null;
            bool closePressed = action != null
                ? action.WasPressedThisFrame()
                : Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

            if (closePressed) Close();
        }

        /// <summary>Closes the overlay. Connect this to the UI close button.</summary>
        public void Close()
        {
            SetVisible(false);
            channel?.HideReadable();
        }

        private void HandleCommand(InteractionUICommand command)
        {
            if (command.Type == InteractionUICommandType.ShowReadable)
                Show(command.Readable, command.ReadableSource);
            else if (command.Type == InteractionUICommandType.HideReadable) SetVisible(false);
        }

        private void Show(ReadableContentDefinition definition, Object source)
        {
            if (definition == null) return;

            if (IsVisible) SetVisible(false);
            activeDefinition = definition;
            activeSource = source;

            // Readables are modal interaction content, so stale world-space
            // interaction feedback must not remain visible behind the overlay.
            channel?.HidePrompt();
            channel?.HideProgress();

            if (titleText != null)
            {
                titleText.text = definition.Title;
                titleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(definition.Title));
            }
            if (bodyText != null) bodyText.text = definition.Body;
            if (closeLabelText != null)
                closeLabelText.text = string.IsNullOrWhiteSpace(definition.CloseLabel)
                    ? defaultCloseLabel : definition.CloseLabel;
            if (backdrop != null)
            {
                Color color = backdrop.color;
                color.a = backdropAlpha;
                backdrop.color = color;
            }
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (IsVisible == visible) return;
            IsVisible = visible;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
            if (visible) EnterModalContext(); else ExitModalContext();
            if (visible) onOpened?.Invoke(); else onClosed?.Invoke();
            if (visible)
            {
                channel?.NotifyReadableOpened(activeDefinition, activeSource);
            }
            else
            {
                channel?.NotifyReadableClosed(activeDefinition, activeSource);
                activeDefinition = null;
                activeSource = null;
            }
        }

        private void EnterModalContext()
        {
            if (blockedInput != InputBlockGroups.None && InputModeManager.Instance != null)
            {
                inputBlockHandle = InputModeManager.Instance.AcquireInputBlock(blockedInput, name);
            }

            hiddenObjectStates.Clear();
            if (hiddenWhileVisible == null) return;

            foreach (GameObject target in hiddenWhileVisible)
            {
                if (target == null || hiddenObjectStates.ContainsKey(target)) continue;
                hiddenObjectStates.Add(target, target.activeSelf);
                target.SetActive(false);
            }
        }

        private void ExitModalContext()
        {
            inputBlockHandle?.Dispose();
            inputBlockHandle = null;

            foreach (KeyValuePair<GameObject, bool> state in hiddenObjectStates)
            {
                if (state.Key != null) state.Key.SetActive(state.Value);
            }
            hiddenObjectStates.Clear();
        }
    }
}
