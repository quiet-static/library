# Persistent Systems and Bootstrap

## Create the systems scene

1. Create an empty scene named for its responsibility, such as `Systems`.
2. Add one instance of each manager the game uses. The supplied manager prefabs are
   useful starting points.
3. Keep manager objects in this scene; gameplay scene objects should call handlers,
   channels, or UnityEvents instead of holding manager references.
4. Create a `SceneBootstrapProfile` and assign the systems scene plus the first
   content scene.
5. Put the bootstrap scene first in Build Settings and include every referenced scene.

Use `SceneBootstrapper` when entering through a small dedicated bootstrap scene. Its
profile decides which persistent and initial content scenes load. Avoid placing a
second copy of a singleton manager in content scenes.

## Verify

- Enter Play Mode from the bootstrap scene.
- Confirm the systems scene remains loaded after content changes.
- Confirm only one instance of every `ToolkitSingleton` exists.
- Run the toolkit validation tools before creating a build.
