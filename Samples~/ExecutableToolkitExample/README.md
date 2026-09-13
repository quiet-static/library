# Executable Toolkit Example

This is a complete, consumer-independent vertical slice:

1. `QuietStaticSampleBootstrap` loads the bootstrap profile.
2. The profile loads `QuietStaticSampleSystems` as a persistent scene.
3. The persistent scene owns scene flow, flags, objectives, the EventSystem, and
   objective UI.
4. `QuietStaticSampleContent` becomes active and presents an **Inspect sample** button.
5. The button invokes `Interactable.Interact()`, sets
   `quietstatic.sample.inspected`, completes the active objective, and refreshes the
   persistent status UI.

## Recommended setup

Choose **Tools > Quiet Static > Samples > Import and Open Executable Example**.
The command imports this sample if needed, requires every sample scene name to resolve
exactly once, places Bootstrap, Systems, and Content first in Build Settings, and opens
the Bootstrap scene. Press Play, then click **Inspect sample**.

You may also import **Executable Toolkit Example** from the Package Manager Samples
tab, then run the same setup command. Existing imported sample files are not
overwritten automatically.

The `Data` folder contains the fully assigned bootstrap profile, flag database,
objective definition/database, and scene-flow request channel. The sample intentionally
uses only package assets, declared package dependencies, and Unity built-in resources.
