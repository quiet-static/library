# Cinematic trees

`CinematicTree` stores an ordered sequence of text beats. Playback always starts at the first beat and moves forward through the list. Every beat has a stable ID, text, characters-per-second reveal speed, and a hold duration measured after typing finishes. There are no speaker names, choices, flags, or branch links.

## Authoring tools

Open **Tools > Quiet Static > Cinematics > Cinematic Trees**, double-click a tree asset, or select the **Cinematic Trees** tab in **Tools > Quiet Static > Workspace** and choose **Open Linear Graph**.

The window provides a searchable asset browser, a left-to-right linear graph, a playback-order list, and the selected beat's fields. Use **Create**, **Add Beat**, **Duplicate**, the up/down buttons, and **Delete** to author the sequence. Changes support Undo/Redo. **Save** writes the asset; **Validate** checks text, IDs, and timing. **Preview Text** previews the selected beat's reveal without starting the game.

Reordering preserves IDs; duplication generates a new ID. Keep IDs unchanged after scene cues refer to them. Deleting a beat or manually changing its ID requires updating the corresponding scene cues; missing cue IDs remain visible as **Missing** in the Inspector rather than silently remapping to a different beat.

You can also create an asset through **Assets > Create > Quiet Static Toolkit > Cinematics > Cinematic Tree**. Use the workspace to add its first beat.

**Export JSON** preserves IDs, text, timing, and order. **Import JSON** validates the complete document before creating a new asset. A minimal document is:

```json
{
  "schemaVersion": 1,
  "description": "Opening narration",
  "beats": [
    {
      "id": "opening.exterior",
      "text": "Your opening narration.",
      "charactersPerSecond": 28,
      "holdSeconds": 2
    }
  ]
}
```

## Scene setup

Drag `Runtime/UI/Prefabs/CinematicTextOverlay.prefab` from the toolkit package into a scene. The prefab contains its own Canvas, typewriter text presentation, `CinematicTreePlayer`, and `CinematicTreeShotCues`.

Assign the tree on the player. Call **Play** from a UnityEvent or enable **Play On Start**. In Play Mode, its Inspector has **Play**, **Advance**, and **Stop** controls. The player implements `ICinematicWaitSource`, so it can also be assigned to a `CutsceneSequenceRunner` step's **Wait Source** or a `CinematicScenePlayer` activity binding.

**Auto Advance** waits until the text is revealed, then holds it for the beat's duration. Disable it for manual progression. Advance first reveals the entire current line, then a subsequent advance moves to the next beat. By default Space, Enter, or left click advances; disable **Allow Default Input** for unattended playback. An assigned input action takes precedence and should be enabled by your existing input setup.

Both typing and holds use unscaled time by default. **Stop**, disabling the player, or finishing the sequence clears the overlay. **On Finished** fires on natural completion; **On Stopped** covers both natural completion and cancellation. Beat-start and beat-finish events include the stable beat ID. Text supports TMP rich text without typing formatting tags as visible characters.

## Camera cues

On `CinematicTreeShotCues`, assign the text player and add a cue per beat that changes presentation. Choose **Text Beat** from the assigned tree, assign a camera director, then choose **Camera Shot** from that director's shot dropdown. **On Beat Entered** can invoke scene audio, lighting, or other reactions. Leaving a shot blank keeps the current shot.

Text assets remain scene-independent: camera references and event reactions live on the scene cue component. IDs distinguish beats even when several beats use the same text or camera shot.

## Presentation

The overlay is bottom-centred and does not intercept clicks. Its Canvas sorts above the cinematic bars (850 versus 800) and below the example screen fader (1000). The included prefab uses the Stolen project's interaction-message font and material and a proportional bottom-screen text area, suitable for the lower cinematic bar. You can change the text font, layout, and Canvas order on a prefab variant.

The toolkit typewriter uses TMP's visible-character count, so no third-party text-animation dependency is required. It does not interpret vendor-specific text-animation action tags.
