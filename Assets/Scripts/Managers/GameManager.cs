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

    // ── Mode & index ─────────────────────────────────────────────────────────────
    public int  CurrentLevelIndex        { get; private set; }
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

    // Called only when auto-bootstrapped. Starts endless mode in whatever scene is already loaded.
    private void BootstrapCurrentScene()
    {
        if (FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None).Length == 0) return;
        IsEndlessMode        = true;
        CurrentEnemyBudget   = BaseEnemyBudget;
        _sessionStartTime    = Time.time;
        _levelStartTime      = Time.time;
        SpawnEnemies();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (CursorManagerPrefab != null && CursorManager.Instance == null)
            Instantiate(CursorManagerPrefab);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
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

        Debug.Log($"[GameManager] SpawnEnemies — budget={CurrentEnemyBudget}, spawners={spawners.Length}, pool={EnemyPool.Length}");

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

        Debug.Log($"[GameManager] Spawned {spawned} enemies.");
    }

    protected override void OnStart() => SetState<St_Gm_Idle>();

    // ── Public API ────────────────────────────────────────────────────────────────

    public void LoadMainMenu()                  => RequestTransition(MainMenuScene);
    public void LoadIntro()                     => RequestTransition(IntroScene);
    public void LoadOutro()                     => RequestTransition(OutroScene);
    public void LoadScene(SceneReference scene) => RequestTransition(scene);

    public void LoadLevel(int index)
    {
        if (index < 0 || index >= LevelScenes.Length) return;
        CurrentLevelIndex = index;
        RequestTransition(LevelScenes[index]);
    }

    /// <summary>
    /// In tutorial mode: advances to the next tutorial level, or starts endless when done.
    /// In endless mode: scores the cleared level and loads the next one.
    /// </summary>
    public void LoadNextLevel()
    {
        if (IsEndlessMode)
        {
            AdvanceEndlessLevel();
            return;
        }

        int next = CurrentLevelIndex + 1;
        if (next < LevelScenes.Length)
        {
            LoadLevel(next);
        }
        else
        {
            TutorialComplete = true;
            StartEndlessMode();
        }
    }

    public void RestartLevel() => LoadLevel(CurrentLevelIndex);

    /// <summary>
    /// Called by the death screen. Skips tutorial if it was already completed this session.
    /// Resets run stats but preserves TutorialComplete.
    /// </summary>
    public void RestartGame(PlayerController player)
    {
        Score                = 0;
        EnemiesKilled        = 0;
        EndlessLevelsCleared = 0;
        player.CurrentHealth = player.MaxHealth;

        if (TutorialComplete)
            StartEndlessMode();
        else
            LoadLevel(0);
    }

    /// <summary>Initialises and enters endless mode from scratch.</summary>
    public void StartEndlessMode()
    {
        IsEndlessMode            = true;
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

    private void RequestTransition(SceneReference scene)
    {
        if (Current is St_Gm_Transitioning) return;
        string path = scene?.ScenePath;
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[GameManager] RequestTransition called with an unassigned SceneReference. Assign it in the Inspector.");
            return;
        }
        PendingScenePath = path;
        SetState<St_Gm_Transitioning>();
    }
}
