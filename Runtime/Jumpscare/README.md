# Jumpscares

`JumpscareTrigger` detects the player and starts a `JumpscareEvent`. The event coordinates
visibility, audio, timing, and optional screen fading while broadcasting lifecycle events.

```text
Jumpscare Root
├── JumpscareEvent
├── Scare Visual (initially inactive)
├── AudioSource
└── Trigger Volume
    └── JumpscareTrigger
```

Keep story consequences in event callbacks or handlers so the reusable sequence remains
independent of flags, objectives, and scene loading.

`CustomJumpscare.prefab` is a neutral starting point with a separate visual anchor,
2D audio source, flash overlay, reveal light, particle effect, and trigger volume.
Replace or remove any optional child without changing the sequence component.

The sequence exposes anticipation, reveal, cleanup, and finish events. Optional reveal
objects, animator triggers, particles, lights, randomized stingers, overlay flash,
camera shake, screen fading, recovery timing, and scaled/unscaled time can be combined
per instance. Flash and camera shake automatically respect reduced-flashing and
reduced-camera-motion preferences when accessibility handling is enabled.

Triggers can also be invoked from a UnityEvent through `Trigger()`. Reusable triggers
support cooldowns, activation limits, probability, and `ResetTrigger()`.
