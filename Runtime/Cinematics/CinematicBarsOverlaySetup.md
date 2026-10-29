# Cinematic bars overlay

Drag `Runtime/UI/Prefabs/CinematicBarsOverlay.prefab` from the Quiet Static Library package into a cinematic scene as a root object. It includes its own screen-space overlay Canvas and two black Images. No camera or EventSystem is required. The bars do not intercept clicks.

Each bar defaults to 12% of the screen height and scales with the screen at any resolution. Adjust **Bar Height** on `CinematicBarsOverlay` to change the framing. **Transition Duration** controls the slide animation and uses unscaled time, but freezes while the Pause Manager is paused.

For a cinematic-only scene, leave **Show On Enable** checked. The bars appear immediately when the scene opens.

For cutscenes within gameplay, assign **Sequence Runner** or **Scene Player** on the overlay. It follows playback automatically and hides when playback finishes, stops, or the source is disabled. If both are assigned, either playing source keeps the bars visible. Assigning a source overrides manual Show/Hide commands.

For custom event-driven scenes, leave both sources empty, uncheck **Show On Enable**, and connect the scene's start/end UnityEvents to `Show()` / `Hide()`. `ShowInstant()` / `HideInstant()` skip the transition. Keep the overlay enabled while hiding so it can animate; for interrupted custom cutscenes, also call Hide from your cancellation event.

The Canvas sorting order is 800, below the example screen fader (1000) and readable overlay (900). Adjust it relative to your own HUD and subtitle canvases as needed. The prefab remains scene-local; place it in a persistent UI scene only when you want to reuse the same instance across scenes.
