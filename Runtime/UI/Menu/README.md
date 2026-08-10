# Menu

`GameQuitter` exposes a UnityEvent-friendly quit action. In the Editor it stops Play Mode;
in a player build it requests application exit.

Wire a button's `On Click` event to `GameQuitter.QuitGame`.

`TitleMenu.prefab` provides Start Game, Settings, and Exit Game navigation. Connect
`TitleMenuView.StartRequested` to the containing game's scene-flow handler. The prefab
uses a full-screen, presentation-neutral layout so games can replace colors, fonts,
backgrounds, and spacing through prefab variants without changing navigation code.

`PauseMenu.prefab` provides Resume, Settings, and Exit Game navigation. It is
designed to be loaded as the additive pause UI scene managed by `PauseManager`,
or placed under an existing pause Canvas. Its nested settings page uses the
same persistent `SettingsManager` as the standalone `SettingsMenu.prefab`.

All supplied menu labels use TextMeshPro. Host scenes should contain an `EventSystem`
with `InputSystemUIInputModule`; the package does not require `StandaloneInputModule`.
