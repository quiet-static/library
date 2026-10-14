using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>Owns a stack of non-interactive overlays in the persistent Player scene.</summary>
    [DisallowMultipleComponent]
    public sealed class ScreenOverlayController : MonoBehaviour
    {
        [Serializable]
        public sealed class Layer
        {
            [Tooltip("Unique name used by channel Show, Hide and Pulse events.")]
            public string id;
            [Tooltip("Optional transparent image, e.g. blood splatter. Empty draws a solid color.")]
            public Sprite sprite;
            public Color color = Color.black;
            [Tooltip("Draw top and bottom bars instead of a fullscreen image.")]
            public bool bars;
            [Range(0f, 0.45f)] public float barHeight = 0.12f;
            [Min(0f)] public float fadeSeconds = 0.2f;
            [Min(0f)] public float holdSeconds = 0.2f;
            [NonSerialized] internal CanvasGroup group;
            [NonSerialized] internal bool visible;
            [NonSerialized] internal float remaining = -1f;
        }

        [Tooltip("Shared asset targeted by dialogue beats, flag checks and scene events.")]
        [SerializeField] private ScreenOverlayChannel channel;
        [Tooltip("Later entries draw over earlier entries. All start hidden.")]
        [SerializeField] private Layer[] layers = Array.Empty<Layer>();
        [Tooltip("Keep below the scene-transition fader so blackouts cover all effects.")]
        [SerializeField] private int sortingOrder = 90;
        private readonly Dictionary<string, Layer> lookup = new(StringComparer.Ordinal);

        private void Awake()
        {
            var root = new GameObject("Overlay Canvas", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            foreach (var layer in layers)
            {
                if (layer == null || string.IsNullOrWhiteSpace(layer.id) || lookup.ContainsKey(layer.id))
                {
                    Debug.LogWarning("Overlay IDs must be nonempty and unique.", this);
                    continue;
                }
                lookup.Add(layer.id, layer);
                var container = new GameObject(layer.id, typeof(RectTransform), typeof(CanvasGroup));
                container.transform.SetParent(root.transform, false);
                Stretch(container.GetComponent<RectTransform>(), 0f, 1f);
                layer.group = container.GetComponent<CanvasGroup>();
                layer.group.alpha = 0f;
                layer.group.blocksRaycasts = layer.group.interactable = false;
                if (layer.bars)
                {
                    float height = Mathf.Clamp(layer.barHeight, 0f, 0.45f);
                    CreateImage(container.transform, layer, 0f, height);
                    CreateImage(container.transform, layer, 1f - height, 1f);
                }
                else CreateImage(container.transform, layer, 0f, 1f);
            }
        }

        private void OnEnable() { if (channel != null) channel.CommandRequested += Handle; }
        private void OnDisable()
        {
            if (channel != null) channel.CommandRequested -= Handle;
            ClearAll();
        }

        private void Update()
        {
            foreach (var layer in lookup.Values)
            {
                float target = layer.visible ? 1f : 0f;
                layer.group.alpha = layer.fadeSeconds <= 0f ? target :
                    Mathf.MoveTowards(layer.group.alpha, target, Time.unscaledDeltaTime / layer.fadeSeconds);
                if (layer.visible && layer.remaining >= 0f && layer.group.alpha >= 1f)
                {
                    layer.remaining -= Time.unscaledDeltaTime;
                    if (layer.remaining <= 0f) layer.visible = false;
                }
            }
        }

        private void Handle(ScreenOverlayCommand command)
        {
            if (command.Id == null) { ClearAll(); return; }
            if (!lookup.TryGetValue(command.Id, out var layer))
            {
                Debug.LogWarning($"Unknown overlay '{command.Id}'.", this);
                return;
            }
            layer.visible = command.Visible;
            layer.remaining = command.Timed ? Mathf.Max(0f, layer.holdSeconds) : -1f;
        }

        /// <summary>Immediately resets every overlay, including pending pulse timers.</summary>
        public void ClearAll()
        {
            foreach (var layer in lookup.Values)
            { layer.visible = false; layer.remaining = -1f; layer.group.alpha = 0f; }
        }

        private static void CreateImage(Transform parent, Layer layer, float minY, float maxY)
        {
            var image = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            Stretch(image.rectTransform, minY, maxY);
            image.sprite = layer.sprite;
            image.color = layer.color;
            image.raycastTarget = false;
        }

        private static void Stretch(RectTransform rect, float minY, float maxY)
        {
            rect.anchorMin = new Vector2(0f, minY);
            rect.anchorMax = new Vector2(1f, maxY);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
