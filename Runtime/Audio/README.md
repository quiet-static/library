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

When an additive content scene cannot reference the persistent handler hierarchy, use
an `AudioRequestChannel` asset. `AudioRequestChannelListener` receives its typed
`AudioCommand` stream and forwards commands to the music and SFX managers.

Use `NPCFootsteps` on an NPC's movement root with an audio request channel and
footstep clips. It measures horizontal travel in LateUpdate, so NavMesh and scripted
movement share the same cadence. Standing still, pausing, enabling an NPC, and large
teleports reset the stride without playing a sound. Tune `stepDistance` for the gait;
the default matches the player's walking cadence at 1.8 units per second.
