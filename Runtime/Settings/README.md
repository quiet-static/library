# Settings

`QuietStatic.SettingsManager` is the authoritative settings owner. Keep one
instance in the persistent System scene. It owns saved values and applies them to
audio, display, post-processing, accessibility, and gameplay systems; it does not own
menu widgets. `SettingsMenuView` binds scene or prefab controls to its public setters.

## Reusable accessibility menu

The package includes `SettingsMenu.prefab`, `TitleMenu.prefab`, `PauseMenu.prefab`, and an
`InputRebindControl.prefab` row under `Runtime/UI/Prefabs`. All player-facing text uses
TextMeshPro. Put the settings
manager and `InputBindingOverridesLoader` in the persistent Systems scene.
Assign the game's InputActionAsset to the loader, then add one rebind row per
action and select its action reference and binding index.

`SettingsManager.OnSettingChanged` publishes a `GameSettingId` after every
change. Managers can subscribe directly and read the typed property from
`SettingsManager.Instance`. Scene-authored reactions can instead use a
`SettingsChangeRelay`. `AccessibilitySettingsApplier` covers common component
switches, subtitle sizing, speaker labels, and high-contrast prompt events.

The starter menu exposes master, music, SFX, look sensitivity, and VSync. Extend or
replace `SettingsMenuView` when a project needs the manager's additional accessibility,
resolution, brightness, ambience, or dialogue settings. The audio mixer may expose
`AmbienceVolume` and `DialogueVolume` in addition to master, music, and SFX. Missing
mixer parameters are harmless, allowing projects to adopt controls gradually.

Use `ClosedCaptionPresenter.ShowCaption(string)` from meaningful sound events.
It automatically suppresses captions when the player's preference is off.
