# Sample scenes

Import **Toolkit Examples** from the package's Samples tab in Package Manager, or open
the scenes in place while developing the package. Imported copies are safe to customize
under the consuming project's `Assets/Samples/Quiet Static Library` folder.

- `Bootstrapper` demonstrates the entry scene.
- `UI` contains reusable interface roots.
- `SceneOrchestrator` demonstrates additive scene coordination.
- `PrefabExamples` groups audio, environment, interaction, and player examples.
- `SystemsAndMenusExample` demonstrates persistent settings and reusable menu prefabs.
- `NarrativeHorrorExample` demonstrates story, tension, and customizable jumpscare wiring.
- `InteractionObjectiveExample` demonstrates a scene-local interaction and project-owned objective asset.

`Definitions` contains intentionally generic ScriptableObject examples referenced by
the new scenes. Some project-owned references—input actions, mixer parameters, art,
audio, and Build Settings scenes—remain deliberately unassigned.

## Coverage

| Sample | Components and authoring pattern |
| --- | --- |
| `SystemsAndMenusExample` | `SettingsManager`, settings and pause prefabs, persistent UI ownership |
| `NarrativeHorrorExample` | `StorySequenceRunner`, `HorrorTensionController`, reusable narrative/tension definitions, customizable jumpscare prefab |
| `InteractionObjectiveExample` | `Interactable`, `ObjectiveManager`, and a reusable objective definition without content-to-manager references |
| `Bootstrapper` / `SceneOrchestrator` | Persistent-scene bootstrap, additive content ownership, and scene-flow composition |
| `PrefabExamples` | Reusable audio, environment, interaction, and player prefab composition |

The definition assets use generic stable IDs so database-backed fields and custom
inspectors have realistic data to display. References belonging to the host game remain
empty by design; each module README calls out those assignments explicitly.

Copy patterns from these scenes rather than editing the samples in place. A typical
play session starts in `Bootstrapper`, loads `System` and `UI`, then loads one replaceable
content scene.
