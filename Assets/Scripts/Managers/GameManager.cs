using System.Collections.Generic;
using Tymski;
using UnityEngine;
using UnityEngine.SceneManagement;
using hoZer;

/// <summary>
/// Persistent singleton that owns scene transitions and tracks game state.
/// Survives every scene load via DontDestroyOnLoad.
/// </summary>
public class GameManager : StateMachine<GameManager>
{
    public static GameManager Instance { get; private set; }

    [Header("Scene References")]
    public SceneReference MainMenuScene;
    public SceneReference IntroScene;
    public SceneReference OutroScene;
    public SceneReference[] LevelScenes;         // tutorial levels, index 0 = first
    public SceneReference[] EndlessLevelScenes;  // 10 preset maps for endless mode

    [Header("UI")]
    public GameObject CursorManagerPrefab;

    [Header("Transition")]
    public ScreenFader Fader;
    public SceneSwiper Swiper;

    [Header("Tutorial Skip")]
    [Tooltip("First scene after the tutorial (e.g. Battle 1). Reaching it marks the tutorial " +
             "complete; once it has been, Play skips straight here instead of replaying the tutorial.")]
    public SceneReference PostTutorialScene;

    // Index into LevelScenes of the level currently loaded (or most recently played).
    public int CurrentLevelIndex { get; private set; } = 0;

    [Header("Enemies")]
    [Tooltip("All enemy prefabs available for endless mode spawning. SpawnCost on each prefab controls budget usage.")]
    public EnemyController[] EnemyPool;

    [Header("Endless – Budget")]
    [Tooltip("Enemy budget on the very first endless level.")]
    public float BaseEnemyBudget      = 3f;
    [Tooltip("Flat budget added per level cleared.")]
    public float BudgetPerLevel       = 1.5f;
    [Tooltip("Budget added per second of total session time (passive growth).")]
    public float BudgetGrowthPerSec   = 0.05f;
    [Tooltip("Clearing a level faster than this (seconds) grants a budget bonus.")]
    public float FastClearThreshold   = 45f;
    [Tooltip("Extra budget awarded for a fast clear.")]
    public float FastClearBonusBudget = 3f;
    [Tooltip("How far enemies can scatter from their spawn point when multiple land on the same one.")]
    public float SpawnScatter         = 0.6f;

    [Header("Scoring")]
    public int ScorePerKill  = 10;
    public int ScorePerLevel = 100;

    // ── Scene state ───────────────────────────────────────────────────────────────
    public enum SceneState { Unknown, MainMenu, Cutscene, Tutorial, Endless }
    public SceneState CurrentSceneState { get; private set; } = SceneState.Unknown;

    public bool IsMainMenu => CurrentSceneState == SceneState.MainMenu;

    // ── Mode & index ─────────────────────────────────────────────────────────────
    public bool TutorialComplete         { get; private set; }
    public bool IsEndlessMode            { get; private set; }
    public int  CurrentEndlessLevelIndex { get; private set; } = -1;

    // ── Stats (reset each new run) ────────────────────────────────────────────────
    public int   Score                { get; private set; }
    public int   EnemiesKilled        { get; private set; }
    public int   EndlessLevelsCleared { get; private set; }
    public float CurrentEnemyBudget   { get; private set; }

    // Written by the public API; read by St_Gm_Transitioning.
    public string PendingScenePath { get; set; }

    private float _sessionStartTime;
    private float _levelStartTime;

    private AsyncOperation _preloadedOp;
    private string         _preloadedPath;

    // Loaded via RuntimeInitializeOnLoadMethod when running a scene directly in the editor.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance == null)
        {
            var prefab = Resources.Load<GameManager>("GameManager");
            if (prefab == null) return;
            Instantiate(prefab);
        }

        // If GameManager exists but hasn't started a proper game flow yet, bootstrap
        // the current scene so it works when played directly from the editor.
        if (!Instance.IsEndlessMode && !Instance.TutorialComplete)
            Instance.BootstrapCurrentScene();
    }

    // Called only when auto-bootstrapped. Detects what kind of scene we landed in and sets state.
    private void BootstrapCurrentScene()
    {
        string path = SceneManager.GetActiveScene().path;

        if (MainMenuScene != null && path == MainMenuScene.ScenePath)
        {
            CurrentSceneState = SceneState.MainMenu;
            return;
        }

        if (IntroScene != null && path == IntroScene.ScenePath ||
            OutroScene != null && path == OutroScene.ScenePath)
        {
            CurrentSceneState = SceneState.Cutscene;
            return;
        }

        if (LevelScenes != null)
        {
            for (int i = 0; i < LevelScenes.Length; i++)
            {
                if (LevelScenes[i] != null && path == LevelScenes[i].ScenePath)
                {
                    CurrentSceneState = SceneState.Tutorial;
                    CurrentLevelIndex = i;
                    return;
                }
            }
        }

        // Anything else (e.g. Battle 1) runs on its own placed enemies / spawner — nothing to set up.
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= HandleSceneLoaded;
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

    // Reaching the first post-tutorial scene — however we got there (goal flag, enemy-clear,
    // or the Play-time skip) — records the tutorial as done.
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsBattleScene(scene.path))
        {
            TutorialProgress.Completed = true;
            // Preload the next random battle so the transition on level-clear is instant.
            string next = PickRandomBattlePath();
            if (!string.IsNullOrEmpty(next)) PreloadScene(next);
        }
        else
        {
            // Preload the next tutorial level if there is one.
            int idx = TutorialIndexOf(scene.path);
            if (idx >= 0 && idx + 1 < LevelScenes?.Length)
            {
                string nextPath = LevelScenes[idx + 1]?.ScenePath;
                if (!string.IsNullOrEmpty(nextPath) && !IsBattleScene(nextPath))
                    PreloadScene(nextPath);
            }
        }
    }

    // ── Preloading ────────────────────────────────────────────────────────────────

    private void PreloadScene(string path)
    {
        if (string.IsNullOrEmpty(path) || path == _preloadedPath) return;
        _preloadedPath = path;
        _preloadedOp   = SceneManager.LoadSceneAsync(path);
        if (_preloadedOp != null)
            _preloadedOp.allowSceneActivation = false;
    }

    /// <summary>
    /// Returns the preloaded AsyncOperation if it matches <paramref name="path"/>, and clears it.
    /// Called by St_Gm_Transitioning to skip the load step when the scene is already in memory.
    /// </summary>
    public AsyncOperation TakePreloadedOp(string path)
    {
        if (_preloadedOp == null || _preloadedPath != path) return null;
        var op      = _preloadedOp;
        _preloadedOp   = null;
        _preloadedPath = null;
        return op;
    }

    /// <summary>
    /// Menu "Play" entry point: jump straight to the post-tutorial scene once the tutorial has been
    /// completed before; otherwise start the normal intro -> tutorial flow.
    /// </summary>
    public void StartGame()
    {
        // Skip the tutorial only in a real build (web / prod), and only once it's been completed.
        // In the editor we always run the full Intro -> tutorial flow so it stays testable.
        if (TutorialProgress.Completed && !Application.isEditor)
            LoadRandomBattle(fade: true);
        else
            LoadIntro();

        if (CursorManagerPrefab != null && CursorManager.Instance == null)
            Instantiate(CursorManagerPrefab);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsEndlessMode) SpawnEnemies();
    }

    private void SpawnEnemies()
    {
        if (EnemyPool == null || EnemyPool.Length == 0)
        {
            Debug.LogWarning("[GameManager] EnemyPool is empty — assign enemy prefabs in the Inspector.");
            return;
        }

        // Collect all spawn points with an EnemySpawnPoint component in the loaded scene.
        EnemySpawnPoint[] spawners = Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
        if (spawners.Length == 0)
        {
            Debug.LogWarning("[GameManager] No EnemySpawnPoint components found in this scene. Add the EnemySpawnPoint component to spawn location objects.");
            return;
        }


        if (CurrentEnemyBudget <= 0f)
        {
            Debug.LogWarning("[GameManager] CurrentEnemyBudget is 0 — StartEndlessMode must be called before enemies can spawn.");
            return;
        }

        List<GameObject> points = new List<GameObject>();
        foreach (var s in spawners) points.Add(s.gameObject);

        float remaining = CurrentEnemyBudget;
        int   spawned   = 0;

        while (remaining > 0f)
        {
            var options = new List<EnemyController>();
            foreach (var e in EnemyPool)
                if (e != null && e.spawnCost <= remaining)
                    options.Add(e);

            if (options.Count == 0) break;

            EnemyController choice = options[Random.Range(0, options.Count)];
            Vector2 origin = points[Random.Range(0, points.Count)].transform.position;
            Vector2 pos    = origin + Random.insideUnitCircle * SpawnScatter;
            Instantiate(choice, pos, Quaternion.identity);
            remaining -= choice.spawnCost;
            spawned++;
        }

    }

    protected override void OnStart() => SetState<St_Gm_Idle>();

    // ── Public API ────────────────────────────────────────────────────────────────

    public void LoadMainMenu() { CurrentSceneState = SceneState.MainMenu;  RequestTransition(MainMenuScene); }
    public void LoadIntro()    { CurrentSceneState = SceneState.Cutscene;  RequestTransition(IntroScene);    }
    public void LoadOutro()    { CurrentSceneState = SceneState.Cutscene;  RequestTransition(OutroScene);    }
    public void LoadScene(SceneReference scene)
    {
        // Any explicit hand-off to a Battle scene feeds the random rotation instead, so every entry
        // point (tutorial end, cutscene NextScene, a stray reference) lands on a random Battle.
        if (scene != null && IsBattleScene(scene.ScenePath))
            LoadRandomBattle(fade: true);
        else
            RequestTransition(scene);
    }

    public void LoadLevel(int index)
    {
        if (index < 0 || index >= LevelScenes.Length) return;

        // A "level" slot pointing at a Battle scene also routes into the random rotation.
        if (IsBattleScene(LevelScenes[index]?.ScenePath))
        {
            LoadRandomBattle(fade: true);
            return;
        }

        CurrentSceneState = SceneState.Tutorial;
        CurrentLevelIndex = index;
        RequestTransition(LevelScenes[index]);
    }

    /// <summary>
    /// Advances the flow based on the scene we're actually in: the post-tutorial arena (Battle 1)
    /// reloads itself — it repeats forever — and a tutorial level advances to the next one (or hands
    /// off to Battle 1 after the last). No endless mode, no reliance on a tracked index.
    /// </summary>
    public void LoadNextLevel()
    {
        // While the player is dead, the death flow owns the reload — never advance the level
        // (otherwise a "no enemies left" clear can race the death and skip you to a battle).
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null && (player.Current is St_Pl_OnDeath || player.Current is St_Pl_Dead))
            return;

        string activePath = SceneManager.GetActiveScene().path;

        // Clearing a Battle scene loads another random Battle — the endless arena rotation.
        if (IsBattleScene(activePath))
        {
            LoadRandomBattle(fade: true);
            return;
        }

        // A tutorial level advances to the next one, or hands off to a random Battle after the last.
        int tut  = TutorialIndexOf(activePath);
        int next = tut + 1;
        if (tut >= 0 && next < LevelScenes.Length)
        {
            LoadLevel(next);
        }
        else
        {
            TutorialComplete = true;
            LoadRandomBattle(fade: true);
        }
    }

    public void RestartLevel() => ReloadActiveScene();

    /// <summary>
    /// Called by the death screen. Resets run stats, then reloads the current scene in place — no
    /// transition, no endless / tutorial routing (Battle 1 replays Battle 1).
    /// </summary>
    public void RestartGame(PlayerController player)
    {
        Score                = 0;
        EnemiesKilled        = 0;
        EndlessLevelsCleared = 0;
        OnPlayerDied();
    }

    /// <summary>Initialises and enters endless mode from scratch.</summary>
    public void StartEndlessMode()
    {
        IsEndlessMode            = true;
        CurrentSceneState        = SceneState.Endless;
        CurrentEnemyBudget       = BaseEnemyBudget;
        CurrentEndlessLevelIndex = -1;
        _sessionStartTime        = Time.time;
        _levelStartTime          = Time.time;
        LoadNextEndlessLevel();
    }

    /// <summary>Called by EnemyController when any enemy is destroyed.</summary>
    public void OnEnemyKilled()
    {
        EnemiesKilled++;
        Score += ScorePerKill;
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Internal ──────────────────────────────────────────────────────────────────

    private void AdvanceEndlessLevel()
    {
        float clearTime = Time.time - _levelStartTime;

        // Level-clear bonus scales with how many levels have been cleared.
        Score += ScorePerLevel * (EndlessLevelsCleared + 1);
        EndlessLevelsCleared++;

        // Per-level flat growth + passive time growth + fast-clear bonus.
        float sessionTime  = Time.time - _sessionStartTime;
        CurrentEnemyBudget = BaseEnemyBudget
                           + EndlessLevelsCleared * BudgetPerLevel
                           + sessionTime * BudgetGrowthPerSec;
        if (clearTime < FastClearThreshold)
            CurrentEnemyBudget += FastClearBonusBudget;

        LoadNextEndlessLevel();
    }

    private void LoadNextEndlessLevel()
    {
        if (EndlessLevelScenes == null || EndlessLevelScenes.Length == 0)
        {
            Debug.LogWarning("[GameManager] No endless level scenes assigned.");
            return;
        }

        int next;
        if (EndlessLevelScenes.Length == 1)
        {
            next = 0;
        }
        else
        {
            do { next = Random.Range(0, EndlessLevelScenes.Length); }
            while (next == CurrentEndlessLevelIndex);
        }

        CurrentEndlessLevelIndex = next;
        _levelStartTime          = Time.time;
        RequestTransition(EndlessLevelScenes[next]);
    }

    /// <summary>
    /// Called when a cutscene ends and no explicit NextScene is set on the CutsceneManager.
    /// Routes to the first tutorial level (intro) or main menu (outro).
    /// </summary>
    public void OnCutsceneComplete()
    {
        if (CurrentSceneState != SceneState.Cutscene)
        {
            return;
        }

        if (!IsEndlessMode)
        {
            LoadLevel(0);
        }
        else
        {
            LoadMainMenu();
        }
    }

    /// <summary>
    /// Called when the player dies. Routes based on current game state:
    /// endless → random endless map; tutorial done → start endless; tutorial → restart same level.
    /// </summary>
    /// <summary>
    /// Player death (no transition): in a Battle scene, jump to another random Battle; in a tutorial
    /// level, retry that level in place.
    /// </summary>
    public void OnPlayerDied()
    {
        if (IsBattleScene(SceneManager.GetActiveScene().path))
            LoadRandomBattle(fade: false);
        else
            ReloadActiveScene();
    }

    private void RequestTransition(SceneReference scene)
    {
        if (scene == null || string.IsNullOrEmpty(scene.ScenePath))
        {
            Debug.LogError("[GameManager] RequestTransition called with an unassigned SceneReference. Assign it in the Inspector.");
            return;
        }
        TransitionToPath(scene.ScenePath);
    }

    private void TransitionToPath(string path)
    {
        if (Current is St_Gm_Transitioning) return;
        if (string.IsNullOrEmpty(path)) return;
        PendingScenePath = path;
        SetState<St_Gm_Transitioning>();
    }

    // ── Battle rotation (dynamic) ──────────────────────────────────────────────────

    // Any "Battle*" scene currently in Build Settings counts. Add a Battle scene to the build and it
    // joins the rotation automatically — no inspector list to maintain.
    private static bool IsBattleScene(string scenePath) =>
        !string.IsNullOrEmpty(scenePath) &&
        System.IO.Path.GetFileNameWithoutExtension(scenePath)
            .StartsWith("Battle", System.StringComparison.OrdinalIgnoreCase);

    private static List<string> BattleScenePaths()
    {
        var battles = new List<string>();
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (IsBattleScene(path)) battles.Add(path);
        }
        return battles;
    }

    // A random Battle scene path, avoiding the current one when there's more than one to choose from.
    private static string PickRandomBattlePath()
    {
        List<string> battles = BattleScenePaths();
        if (battles.Count == 0) return null;
        if (battles.Count == 1) return battles[0];

        string current = SceneManager.GetActiveScene().path;
        string pick;
        do { pick = battles[Random.Range(0, battles.Count)]; }
        while (pick == current);
        return pick;
    }

    /// <summary>Loads a random Battle scene from Build Settings. fade=true uses the swipe/fade
    /// transition (level clear); fade=false loads directly (death reset, no transition).</summary>
    private void LoadRandomBattle(bool fade)
    {
        // Prefer the already-preloaded battle so the transition activates an in-memory scene.
        string pick = (!string.IsNullOrEmpty(_preloadedPath) && IsBattleScene(_preloadedPath))
            ? _preloadedPath
            : PickRandomBattlePath();

        if (string.IsNullOrEmpty(pick))
        {
            Debug.LogError("[GameManager] No 'Battle*' scenes found in Build Settings — add at least one.");
            return;
        }

        if (fade) TransitionToPath(pick);
        else      SceneManager.LoadScene(pick);
    }

    /// <summary>Reloads the active scene directly (no fade transition).</summary>
    public void ReloadActiveScene() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    // True when an active scene path is the scene a SceneReference points at (full path or name).
    private static bool PathMatches(string scenePath, SceneReference reference)
    {
        if (reference == null) return false;
        string target = reference.ScenePath;
        if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(scenePath)) return false;
        return scenePath == target ||
               System.IO.Path.GetFileNameWithoutExtension(scenePath)
                 == System.IO.Path.GetFileNameWithoutExtension(target);
    }

    // Index of the tutorial level matching the active scene path, or -1 if it isn't one.
    private int TutorialIndexOf(string scenePath)
    {
        if (LevelScenes == null) return -1;
        for (int i = 0; i < LevelScenes.Length; i++)
            if (PathMatches(scenePath, LevelScenes[i])) return i;
        return -1;
    }
}
