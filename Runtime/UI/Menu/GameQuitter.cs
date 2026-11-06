using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    [Header("Shutdown")]
    [Tooltip("Unload scenes and release their resources before closing the player. Useful for gameplay menus with additive scenes.")]
    [SerializeField] private bool unloadScenesBeforeQuit;

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

        if (QuietStatic.Toolkit.Input.KeyboardShortcut.WasReleasedThisFrame(quitKey))
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
        if (unloadScenesBeforeQuit) GameQuitShutdown.Request();
        else Application.Quit();
#endif
    }
}

/// <summary>Keeps orderly player teardown alive after its originating menu is unloaded.</summary>
internal sealed class GameQuitShutdown : MonoBehaviour
{
    private static bool requested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => requested = false;

    internal static void Request()
    {
        if (requested) return;
        requested = true;
        var runner = new GameObject("Application Shutdown").AddComponent<GameQuitShutdown>();
        DontDestroyOnLoad(runner.gameObject);
        runner.StartCoroutine(runner.Shutdown());
    }

    private IEnumerator Shutdown()
    {
        Debug.Log("[GameQuitter] Orderly shutdown started.");
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // An empty active scene lets Unity unload every content and service scene.
        Scene empty = SceneManager.CreateScene("Application Shutdown");
        SceneManager.SetActiveScene(empty);
        var scenes = new System.Collections.Generic.List<Scene>();
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (scene != empty) scenes.Add(scene);
        }
        // Content and overlays unload first; persistent services remain until last.
        for (int index = scenes.Count - 1; index >= 0; index--)
        {
            if (!scenes[index].isLoaded) continue;
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scenes[index]);
            if (unload != null) yield return unload;
        }
        yield return Resources.UnloadUnusedAssets();
        System.GC.Collect();
        yield return null;
        Debug.Log("[GameQuitter] Scene teardown complete. Exiting player.");
        Application.Quit(0);
    }
}
