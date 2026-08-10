# Codebase Gap Audit

## Current strengths

The library has clear runtime boundaries for persistent managers, scene handlers,
ScriptableObject definitions, request channels, and stable string IDs. Scene flow,
dialogue, objectives, saving, settings, input modes, horror tension, and jumpscares
all expose UnityEvent-friendly entry points. Edit Mode coverage exercises the main
data and manager contracts.

## Gaps and recommended follow-up

### Project wiring cannot be fully supplied by the package

- Input rebinding rows require each game's `InputActionReference` and binding index.
- Audio volume controls require exposed mixer parameters named `MasterVolume`,
  `MusicVolume`, `SfxVolume`, `AmbienceVolume`, and `DialogueVolume`.
- Scene references must be added to Build Settings and mapped by the project's
  `SceneFlowMap`.
- Dialogue UI, fonts, prompt styling, post-processing profiles, models, animation
  clips, and audio clips remain project-owned assets.

These are intentional integration seams, not missing runtime behavior. The setup
guides identify every required assignment.

### Authoring validation can grow

- Add build-time validation for missing mixer parameters and unassigned input
  actions; Unity does not expose every mixer detail cheaply during normal import.
- Add graph visualization for story stages similar to the scene-flow explorer.
- Add a consolidated accessibility preview window for subtitle sizes, contrast,
  flashing suppression, and motion suppression.
- Add save-data migration hooks before changing serialized save schema in a shipped
  game.

### Test coverage still worth adding

- Play Mode coverage for additive scene transitions and pause-scene loading.
- Play Mode coverage for complete jumpscare timing, screen fading, and cancellation.
- UI navigation tests driven through the EventSystem rather than direct method calls.
- AudioMixer integration tests inside a project containing a configured mixer.
- Build validation on at least one desktop player target.

### Documentation maintenance

Each new Inspector-facing serialized field should have a tooltip, and every new
top-level runtime folder should contain a short README. New samples should avoid
game-specific names and explain which references intentionally remain empty.
