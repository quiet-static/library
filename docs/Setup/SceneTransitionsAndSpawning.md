# Scene Transitions and Spawning

1. Create a `SceneFlowMap` asset.
2. Add connections using stable IDs such as `hall.to.basement`; assign source and
   destination `SceneReference` values. The connection ID is carried into the
   destination as a transient condition.
3. Put `SceneFlowManager` and the screen fader in the persistent systems scene.
4. Add one `SceneTransitionDefinition` to the destination scene. Add an ordered
   response for each incoming connection ID and wire its UnityEvent to the local setup.
5. For placement, register the persistent object with `SpawnTarget`, add a named
   `SpawnPoint` in the destination, and call a `SpawnHandler` from the response event.
6. Add `SceneTransitionHandler` to a scene-local object. Configure its connection ID
   or request data, then connect an interaction, trigger, or UnityEvent to it.

The manager fades to black, loads and activates the destination, unloads the source
when configured, applies the matching destination response, and fades clear. Use the
Scene Flow Explorer to inspect links. The definition keeps route-specific behavior in
the destination scene instead of adding spawning or narrative policy to the manager.

For direct trigger volumes, use `SceneTransitionTrigger`. For cross-scene decoupling,
use `SceneFlowRequestChannel` and its persistent listener.
