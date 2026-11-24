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
## Menu button audio

The starter title, pause, settings, and rebind prefabs use `AudibleButton` for menu
actions. Add one `ButtonAudioFeedback` to the persistent audio scene and assign its click
clip and output mixer group. The component plays the clip when an active, interactable
`AudibleButton` receives a left click or a UI submit, including while listener audio is
paused. Use `AudibleButton` for custom menu actions that should share this feedback.

## Shared progress-bar styling

`ProgressBarTheme` is a reusable ScriptableObject shared by world-space and
screen-space meters. `WorldSpaceProgressBar` applies it directly, while
`ScreenSpaceProgressBarStyle` adapts the same theme to a standard UI `Slider`.
This keeps screen-space interaction meters visually consistent with world-space
meters without changing where either one is rendered.
