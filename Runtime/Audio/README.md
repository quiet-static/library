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

## Random overlays for a music cue

Add `RandomMusicOverlay` to a scene GameObject with a dedicated `AudioSource`.
Assign the clip pool and minimum and maximum delays. Set the source's spatial blend
and mixer group for 2D or positional playback.

Request the music cue through the `AudioRequestChannel`, then call
`overlay.Begin(channel, track)` with the same clip. The emitter waits for the persistent
music source to play that clip before counting down. Pausing the cue suspends the
countdown and pauses any playing overlay. Switching to a different clip stops the
current overlay. Call `End()` when the scene's cue is finished to clear the association.

## NPC footsteps

Use `NPCFootsteps` on an NPC's movement root with an audio request channel and
footstep clips. It measures horizontal travel in LateUpdate, so NavMesh and scripted
movement share the same cadence. Standing still, pausing, enabling an NPC, and large
teleports reset the stride without playing a sound. Tune `stepDistance` for the gait;
the default matches the player's walking cadence at 1.8 units per second.
