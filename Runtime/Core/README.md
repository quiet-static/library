# Core

Core types define shared state and broad notifications.

- `ToolkitEvents` is the cross-system notification hub.
- `GameStateController` stores a strongly typed high-level game state.
- `GameStateDatabase` documents the string state IDs used by legacy/manager workflows.

```text
GameplayManagers
└── GameStateController
```

Subscribe in `OnEnable` and unsubscribe in `OnDisable`. Use direct references or local
UnityEvents for local relationships; reserve `ToolkitEvents` for genuinely global state.
