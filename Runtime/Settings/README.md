# Settings

`QuietStatic.SettingsManager` is the authoritative settings owner. Keep one
instance in the persistent System scene. Its Inspector UI references are
optional; loaded settings menus can call its public setter methods or bind UI
controls to them.

`QuietStatic.Toolkit.Settings.SettingsManager` is retained only so existing
serialized components continue to load. It now lives in the dedicated
Compatibility assembly. Do not add it to new scenes, and do not run both
settings managers at the same time.

## Reusable accessibility menu

The package includes `SettingsMenu.prefab`, `PauseMenu.prefab`, and an
`InputRebindControl.prefab` row under `Runtime/UI/Prefabs`. Put the settings
manager and `InputBindingOverridesLoader` in the persistent Systems scene.
Assign the game's InputActionAsset to the loader, then add one rebind row per
action and select its action reference and binding index.

`SettingsManager.OnSettingChanged` publishes a `GameSettingId` after every
change. Managers can subscribe directly and read the typed property from
`SettingsManager.Instance`. Scene-authored reactions can instead use a
`SettingsChangeRelay`. `AccessibilitySettingsApplier` covers common component
switches, subtitle sizing, speaker labels, and high-contrast prompt events.

The audio mixer may expose `AmbienceVolume` and `DialogueVolume` parameters in
addition to the existing master, music, and SFX parameters. Missing mixer
parameters are harmless, allowing projects to adopt the controls gradually.

Use `ClosedCaptionPresenter.ShowCaption(string)` from meaningful sound events.
It automatically suppresses captions when the player's preference is off.
