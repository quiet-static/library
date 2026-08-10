# Settings, Pause, and Accessibility

1. Put the authoritative `SettingsManager` in the persistent systems scene.
2. Assign the AudioMixer and expose the volume parameters used by the project.
3. Place `SettingsMenu.prefab` under a Canvas, or use the copy nested in
   `PauseMenu.prefab`.
4. Put `InputBindingOverridesLoader` beside the input owner and assign the game's
   `InputActionAsset`.
5. Add one `InputRebindControl` prefab row per rebindable action. Assign its action
   reference and binding index.
6. Use `AccessibilitySettingsApplier` for subtitle text, speaker labels, flicker/head
   bob behaviors, and high-contrast theme events.
7. Call `ClosedCaptionPresenter.ShowCaption(string)` from meaningful sound events.

For pause UI, either load an additive pause scene through `PauseManager`, or place the
prefab beneath persistent UI and activate it from pause-state events. Resume, Settings,
Back, and Exit are already wired in the supplied prefab.

Managers can subscribe to `SettingsManager.OnSettingChanged` and read the typed value
from the singleton. Scene-local components can use `SettingsChangeRelay` instead.
