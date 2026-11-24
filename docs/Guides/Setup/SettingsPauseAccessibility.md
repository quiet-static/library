# Settings, Pause, and Accessibility

1. Put the authoritative `SettingsManager` in the persistent systems scene.
2. Assign the AudioMixer and expose the volume parameters used by the project.
3. Place `SettingsMenu.prefab` under a Canvas, or use the copy nested in
   `TitleMenu.prefab` or `PauseMenu.prefab`. All supplied labels use TextMeshPro.
4. Put `InputBindingOverridesLoader` beside the input owner and assign the game's
   `InputActionAsset`.
5. Add one `InputRebindControl` prefab row per rebindable action. Assign its action
   reference and binding index.
6. Use `AccessibilitySettingsApplier` for subtitle text, speaker labels, flicker/head
   bob behaviors, and high-contrast theme events.
7. Call `ClosedCaptionPresenter.ShowCaption(string)` from meaningful sound events.

The built-in Master, Music, and SFX sliders preview volume changes as you move them.
Choose **Apply** to save pending settings or **Back** to discard them and restore
committed mixer volumes. For click feedback during pause, configure
[menu button audio](../../../Runtime/UI/README.md#menu-button-audio).

For pause UI, load an additive pause scene through `PauseManager` or place the prefab
beneath persistent UI and activate it from pause-state events. Resume, Save Game,
Settings, Back, and Exit are wired in the supplied prefab. Assign the project's
`SaveRequestChannel` to `PauseMenuView` to make Save Game available.

For title UI, connect `TitleMenuView.StartRequested` to project-owned scene flow. Use
prefab variants for art and layout changes rather than adding game-specific behavior to
the library prefab. Add `InputSystemUIInputModule` to the host EventSystem; do not add a
legacy `StandaloneInputModule`.

Managers can subscribe to `SettingsManager.SettingChanged` and read the typed value
from the singleton. Scene-local components can use `SettingsChangeRelay` instead.
