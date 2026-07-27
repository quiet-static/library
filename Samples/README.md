# Sample scenes

- `Bootstrapper` demonstrates the entry scene.
- `System` contains persistent gameplay managers.
- `UI` contains reusable interface roots.
- `SceneOrchestrator` demonstrates additive scene coordination.
- `PrefabExamples` groups audio, environment, interaction, and player examples.

Copy patterns from these scenes rather than editing the samples in place. A typical
play session starts in `Bootstrapper`, loads `System` and `UI`, then loads one replaceable
content scene.
