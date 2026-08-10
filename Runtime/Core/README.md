# Core

Core types define shared state and broad notifications.

- `ToolkitEvents` is the cross-system notification hub.
- `GameStateDatabase` documents the string IDs consumed by manager workflows.

```text
GameplayManagers
`-- GameStateManager
```

`QuietStatic.Toolkit.State.GameStateManager` is the authoritative game-state
owner. Subscribe in `OnEnable` and unsubscribe in `OnDisable`. Use direct
references or local UnityEvents for local relationships; reserve
`ToolkitEvents` for genuinely global state.

Inspector-facing global-state strings marked with `[GameStateId]` use a
searchable selector populated from the project's `GameStateDatabase`. Runtime
values remain ordinary strings. Missing legacy values are retained and shown
with a warning until an author explicitly changes them.

The deprecated enum-based `GameStateController` lives only in the dedicated
Compatibility assembly. Do not reference it from current Runtime or project code.
