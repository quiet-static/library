# Horror tension

`HorrorTensionDefinition` stores scene-independent ambience states. Each state has a
stable ID, priority, flag activation requirement, music action, randomized entry SFX,
and overlay color/alpha/transition time.

Place one `HorrorTensionController` in a persistent scene with a dedicated 2D
`AudioSource`. Optionally assign a persistent overlay `CanvasGroup` and `Image`.
The controller selects the highest-priority matching state whenever flags change and
falls back to the definition's default state. Call `SetState(string)` from a UnityEvent
for explicit changes, or `ReevaluateFromFlags()` to resume rule-based selection.

For example, a state requiring `ReadLetter` and `PowerOut` with priority 20 can change
the music, play a stinger, and darken/tint the overlay as soon as both flags are set.
Use `TensionStateEventRelay` for state-specific scene effects such as flickering lights,
opening doors, enabling props, camera effects, or starting a cinematic.
