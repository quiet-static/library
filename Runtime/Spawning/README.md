# Spawning

`SpawnManager` is the single authoritative spawning path. It resolves named
`SpawnPoint` components, moves registered targets safely, applies a configurable
fallback point, and instantiates prefabs.

```text
GameplayManagers
`-- SpawnManager

Persistent player or scene object
`-- SpawnTarget (target id: Player)

Content Scene
`-- SpawnPoints
    |-- Default (SpawnPoint id: Default)
    `-- Hallway (SpawnPoint id: Hallway)
```

Use `SpawnTarget` to self-register anything that save loading or scene flow must
move. Use `SpawnHandler` on triggers and UnityEvent-facing scene objects.
