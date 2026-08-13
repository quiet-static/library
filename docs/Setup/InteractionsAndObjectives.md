# Interactions and Objectives

## Create an interaction

1. Put an `Interactable` or specialized hold/progress interactable on the target.
2. Configure its prompt, availability, cooldown/reuse behavior, and success events.
3. Add the matching unlock component when flags gate availability.
4. Use handlers or request channels in success events to set flags, start dialogue,
   complete objectives, play audio, or request scene transitions.
5. Ensure the player has an `Interactor`, compatible input source, and interaction UI.

## Create an objective

1. Add a stable entry to `ObjectiveDatabase` and create/assign an
   `ObjectiveDefinition`. Configure its optional activation and completion flags.
2. Keep `ObjectiveManager` in the persistent systems scene.
3. Add `ObjectivePresenter` to the HUD and connect its text/status visuals.
4. Use `ObjectiveHandler` from scene events rather than referencing the manager.
5. Order database entries from lowest to highest activation priority. The manager
   selects the last eligible definition when progression flags change.

Keep objective identity stable after save files exist. Change display text freely;
do not casually rename IDs used by saves, dialogue, or story stages.
