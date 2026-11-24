# Horror Tension and Jumpscares

## Tension states

1. Create a `HorrorTensionDefinition` asset.
2. Add states with stable IDs, priorities, flag requirements, music policy, entry
   stingers, and overlay appearance.
3. Put `HorrorTensionController` in the systems scene and assign a 2D AudioSource and
   optional fullscreen overlay.
4. Allow flag changes to reevaluate automatically, or call `SetState(string)` from a
   UnityEvent.
5. Add `TensionStateEventRelay` to scene objects for local lights, particles, doors,
   or post-processing changes.

## Custom jumpscare

1. Drag `CustomJumpscare.prefab` into a content scene.
2. Replace `ScareVisual` with a model, animated rig, sprite, or UI presentation.
3. Assign one stinger or a randomized pool. Configure anticipation, visible, and
   recovery timing.
4. Add optional reveal objects, animators, particles, lights, flash, shake, and fader.
5. Wire anticipation/reveal/cleanup/finish events to local consequences.
6. Resize the trigger volume, or disable it and call `JumpscareTrigger.Trigger()` from
   another UnityEvent.

Leave accessibility handling enabled unless a non-flashing/non-moving alternative is
provided. Test the scare with both reduced-flashing and reduced-motion settings.
