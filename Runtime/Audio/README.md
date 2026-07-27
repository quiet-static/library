# Audio

Audio helpers cover global music/SFX and localized event-driven playback.

```text
AudioManagers
├── MusicManager
└── SfxManager

World Sound
├── AudioSource (3D spatial blend)
└── EventSound3D
```

Use `AudioHandler` from scene UnityEvents. Use `EventSound3D` or `AudioEventPlayer` for
object-local sounds. Keep mixer routing and volume policy in persistent managers.
