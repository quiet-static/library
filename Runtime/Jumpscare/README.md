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
