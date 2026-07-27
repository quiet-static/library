# Menu

`GameQuitter` exposes a UnityEvent-friendly quit action. In the Editor it stops Play Mode;
in a player build it requests application exit.

Wire a button's `On Click` event to `GameQuitter.Quit`.
