# UI

UI components cover HUD visibility, cursor policy, dialogue/interaction prefabs, credits,
and simple menu helpers.

```text
UI
├── UIControllers
├── dialogue_ui
├── interaction_ui
└── hud
```

Keep the UI scene persistent when transitions should preserve it. Let UI listen to manager
events; avoid making gameplay objects search the canvas hierarchy.
# Shared progress-bar styling

`ProgressBarTheme` is a reusable ScriptableObject shared by world-space and
screen-space meters. `WorldSpaceProgressBar` applies it directly, while
`ScreenSpaceProgressBarStyle` adapts the same theme to a standard UI `Slider`.
This keeps screen-space interaction meters visually consistent with world-space
meters without changing where either one is rendered.
