using UnityEngine;

/// <summary>
/// Provides a simple way to quit the game from either code, UI buttons, UnityEvents,
/// or an optional keyboard shortcut.
/// </summary>
/// <remarks>
/// In a built player, this component calls <see cref="Application.Quit()"/>.
/// In the Unity Editor, it stops Play Mode instead, which makes the same quit flow
/// testable while developing.
///
/// Attach this component to any scene object that should be able to quit the game.
/// For menu usage, connect <see cref="QuitGame"/> to a UI Button's OnClick event.
/// </remarks>
public class GameQuitter : MonoBehaviour
{
    [Header("Keyboard Shortcut")]
    [Tooltip("If true, this component will listen for the configured quit key during Update.")]
    [SerializeField] private bool allowKeyboardQuit = true;

    [Tooltip("Keyboard key that quits the game when released, if keyboard quitting is enabled.")]
    [SerializeField] private KeyCode quitKey = KeyCode.X;

    /// <summary>
    /// Checks for the optional keyboard quit shortcut once per frame.
    /// </summary>
    private void Update()
    {
        if (!allowKeyboardQuit)
        {
            return;
        }

        if (Input.GetKeyUp(quitKey))
        {
            QuitGame();
        }
    }

    /// <summary>
    /// Quits the application.
    /// </summary>
    /// <remarks>
    /// Unity builds quit through <see cref="Application.Quit()"/>.
    /// The Unity Editor does not quit the editor application itself; instead, it exits Play Mode.
    /// </remarks>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
