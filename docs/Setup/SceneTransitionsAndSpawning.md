# Scene Transitions and Spawning

1. Create a `SceneFlowMap` asset.
2. Add connections using stable IDs such as `hall.to.basement`; assign source and
   destination `SceneReference` values and optional destination spawn IDs.
3. Put `SceneFlowManager` and the screen fader in the persistent systems scene.
4. Add a `SpawnTarget` to the destination scene and give it the matching stable ID.
5. Add `SceneTransitionHandler` to a scene-local object. Configure its connection ID
   or request data, then connect an interaction, trigger, or UnityEvent to it.

The manager fades to black, loads the destination, places the player, unloads the
source when configured, and fades clear. Use the Scene Flow Explorer to inspect links
and locate unresolved scenes or spawn IDs.

For direct trigger volumes, use `SceneTransitionTrigger`. For cross-scene decoupling,
use `SceneFlowRequestChannel` and its persistent listener.
