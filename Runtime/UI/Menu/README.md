# Menu

`GameQuitter` exposes a UnityEvent-friendly quit action. In the Editor it stops Play Mode;
in a player build it requests application exit.

Wire a button's `On Click` event to `GameQuitter.Quit`.

`PauseMenu.prefab` provides Resume, Settings, and Exit Game navigation. It is
designed to be loaded as the additive pause UI scene managed by `PauseManager`,
or placed under an existing pause Canvas. Its nested settings page uses the
same persistent `SettingsManager` as the standalone `SettingsMenu.prefab`.
