# Runtime modules

Runtime code is grouped by responsibility. Components are intentionally small and can
be combined without adopting every toolkit system.

- `Core`, `Managers`, and `Handlers` provide global state and event-driven coordination.
- `Flags`, `Objectives`, and `Dialogue` provide narrative progression.
- `Characters`, `Input`, `Cameras`, `Interactions`, and `Minigames` provide
  player-facing gameplay.
- `SceneFlow`, `Spawning`, and `Cinematics` coordinate transitions and sequences.
- `Saving` coordinates versioned slots across flags, scenes, spawn points, and
  explicitly registered state participants.
- `Audio`, `UI`, `Animation`, and `Utilities` provide reusable presentation helpers.

Start with the manager prefabs and sample scenes. Avoid placing a second copy of a
singleton manager in content scenes; use handlers or static events from scene objects.

## Inspector authoring

- Hover serialized fields for setup guidance; components use tooltips to describe
  ownership, optional references, units, and event timing.
- Long player-facing or designer-facing copy uses multi-line text areas. Short IDs,
  labels, tags, and prompts remain single-line so accidental whitespace is visible.
- Numeric fields use ranges for true bounded values (such as normalized weights) and
  minimum constraints for durations, distances, radii, and other nonnegative values.
- Runtime state stays behind read-only properties where practical. Public serialized
  fields remain public only where they are established data contracts or changing them
  would break existing callers.
