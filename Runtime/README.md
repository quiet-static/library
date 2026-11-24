# Runtime modules

Runtime code is grouped by responsibility. Components are intentionally small and can
be combined without adopting every toolkit system.

- [Core](Core/README.md), [Managers](Managers/README.md), [Handlers](Handlers/README.md),
  and [Events](Event/README.md) provide global state and cross-scene communication.
- [Flags](Flags/README.md), [Objectives](Objectives/README.md), [Dialogue](Dialogue/README.md),
  [Narrative](Narrative/README.md), and [Deductions](Deductions/Deductions/README.md) provide narrative progression.
- [Characters](Characters/README.md), [Input](Input/README.md), [Cameras](Cameras/README.md),
  [Interactions](Interactions/README.md), and [Minigames](Minigames/README.md) provide player-facing gameplay.
- [Scene Flow](SceneFlow/README.md), [Spawning](Spawning/README.md), and
  [Cinematics](Cinematics/README.md) coordinate transitions and sequences.
- [Saving](Saving/README.md) coordinates versioned slots across flags, scenes, spawn points, and
  registered state participants.
- [Audio](Audio/README.md), [UI](UI/README.md), [Settings](Settings/README.md),
  [Animation](Animation/README.md), and [Utilities](Utilities/README.md) provide reusable presentation helpers.
- [Horror](Horror/README.md) and [Jumpscares](Jumpscare/README.md) provide tension and scares.
- The HTML [Code reference](../docs/index.html) also covers Pause and Debug Tools.

Cross-scene player activities use `PlayerActivityChannel`, `HoldActivitySequence`, and
`PlayerActivityHandler`.

Start with the manager prefabs and sample scenes. Avoid placing a second copy of a
singleton manager in content scenes; use handlers or ScriptableObject channels from scene objects.

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
