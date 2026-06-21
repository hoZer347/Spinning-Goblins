using Tymski;
using UnityEngine;
using hoZer;

/// <summary>
/// Persistent singleton that owns scene transitions and tracks the current level.
/// Follows the hoZer StateMachine pattern: states handle the async load logic.
/// Survives every scene load via DontDestroyOnLoad.
///
/// Assign all scene references in the Inspector on the GameManager prefab.
/// </summary>
public class GameManager : StateMachine<GameManager>
{
    public static GameManager Instance { get; private set; }

    [Header("Scene References")]
    public SceneReference MainMenuScene;
    public SceneReference IntroScene;
    public SceneReference OutroScene;
    public SceneReference[] LevelScenes; // index 0 = Level 1

    [Header("Transition")]
    public ScreenFader Fader;

    // Index into LevelScenes of the level currently loaded (or most recently played).
    public int CurrentLevelIndex { get; private set; } = 0;

    // Written by the public API; read by St_Gm_Transitioning.
    public string PendingScenePath { get; set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    protected override void OnStart()
    {
        SetState<St_Gm_Idle>();
    }

    // --- Public API ---------------------------------------------------------

    public void LoadMainMenu()                   => RequestTransition(MainMenuScene);
    public void LoadIntro()                      => RequestTransition(IntroScene);
    public void LoadOutro()                      => RequestTransition(OutroScene);
    public void LoadScene(SceneReference scene)  => RequestTransition(scene);

    public void LoadLevel(int index)
    {
        if (index < 0 || index >= LevelScenes.Length) return;
        CurrentLevelIndex = index;
        RequestTransition(LevelScenes[index]);
    }

    /// <summary>Loads the level after the current one, or the outro if on the last level.</summary>
    public void LoadNextLevel()
    {
        int next = CurrentLevelIndex + 1;
        if (next < LevelScenes.Length) LoadLevel(next);
        else LoadOutro();
    }

    public void RestartLevel() => LoadLevel(CurrentLevelIndex);

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // --- Internal -----------------------------------------------------------

    private void RequestTransition(SceneReference scene)
    {
        if (Current is St_Gm_Transitioning) return;
        PendingScenePath = scene.ScenePath;
        SetState<St_Gm_Transitioning>();
    }
}
