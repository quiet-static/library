# Cameras

Camera components separate input-driven rotation, focus targets, authored poses, and
simple orbit behavior.

```text
Player Camera Rig
├── CameraController
├── CameraFocusController
└── Camera

Cutscene Camera
└── CameraPoseDirector
```

Use the first/third-person rig prefabs as references. Scene sequences should request poses
or focus through directors/managers rather than directly rewriting a gameplay camera.
