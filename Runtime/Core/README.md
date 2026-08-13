# Core

Core types define shared state and common runtime infrastructure.

- `GameStateDatabase` documents the string IDs consumed by manager workflows.

```text
GameplayManagers
`-- GameStateManager
```

`QuietStatic.Toolkit.State.GameStateManager` is the authoritative game-state
owner. Subscribe in `OnEnable` and unsubscribe in `OnDisable`. Use direct
references or local UnityEvents for local relationships, and subscribe to the
authoritative component's lifecycle events for global observations.

Inspector-facing global-state strings marked with `[GameStateId]` use a
searchable selector populated from the project's `GameStateDatabase`. Runtime
values remain ordinary strings. Missing legacy values are retained and shown
with a warning until an author explicitly changes them.
