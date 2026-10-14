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
## Clutter and flavor-text interactions

Open **Tools > Quiet Static > Workspace**, then choose **Clutter** in the toolbar for optional environmental observations such as books, cups, photographs, and bedside objects. Select one or more scene objects and choose **Set Up Selected Objects as Clutter Interactables**. The tool adds the standard `Interactable`, `FlavorTextInteraction`, `InteractionHighlighter`, and a trigger collider when needed. It also applies the `Interactables` layer when that layer exists and assigns the first discoverable `InteractionUIChannel` when the component does not already have one.

The resulting `FlavorTextInteraction` publishes a temporary message through the interaction UI channel. It does not use dialogue trees, flags, objectives, or story progression. Existing non-flavor interaction targets appear in a separate read-only list so story and gameplay interactions are not modified accidentally.

Use short observations that can be read without pausing for a conversation. Choose dialogue instead when the content needs speakers, multiple beats, choices, flags, camera focus, or lifecycle events. Choose a readable when the player should open and close a longer letter, journal page, or document.

The setup command is safe to run again. It repairs missing targeting components without adding duplicates. Review the generated trigger bounds around unusually shaped or deeply nested art, and confirm that the player's interaction mask includes the assigned layer.

## Edit content by scene or GameObject

The Content Workspace's **Scene Content** tab provides a loaded-scene dropdown and an optional GameObject scope. Use **Use Selection** to scope the view to the selected object and all of its descendants, or **Clear** to return to the whole scene.

The scoped view groups content into:

- **Flavor Interactions**, with inline prompt, flavor text, and channel editing;
- **Scene Dialogues**, with tree, focus, and speaker fields plus an **Open Graph** shortcut;
- **Other Interaction Targets**, with prompt editing and direct object selection.

The Workspace search field matches object names and full hierarchy paths inside the current scope. Only loaded scenes are available; open a closed scene before trying to inspect or edit its scene objects. Changes use Unity serialization and mark the owning scene dirty, so save the scene normally after editing.
