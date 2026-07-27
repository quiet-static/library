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
