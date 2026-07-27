# Spawning

`SpawnPoint` marks a named destination and `SpawnService` positions registered objects.

```text
Content Scene
└── SpawnPoints
    ├── Entrance (SpawnPoint id: Entrance)
    └── Hallway (SpawnPoint id: Hallway)
```

Use stable, unique IDs within the loaded scene set. Trigger spawning after the destination
scene is loaded so its points are registered.
