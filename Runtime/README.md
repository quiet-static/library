# Runtime modules

Runtime code is grouped by responsibility. Components are intentionally small and can
be combined without adopting every toolkit system.

- `Core`, `Managers`, and `Handlers` provide global state and event-driven coordination.
- `Flags`, `Objectives`, and `Dialogue` provide narrative progression.
- `Characters`, `Input`, `Cameras`, and `Interactions` provide player-facing gameplay.
- `SceneFlow`, `Spawning`, and `Cinematics` coordinate transitions and sequences.
- `Audio`, `UI`, `Animation`, and `Utilities` provide reusable presentation helpers.

Start with the manager prefabs and sample scenes. Avoid placing a second copy of a
singleton manager in content scenes; use handlers or static events from scene objects.
