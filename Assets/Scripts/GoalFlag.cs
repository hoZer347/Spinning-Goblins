using UnityEngine;
using UnityEngine.SceneManagement;
using Tymski;

/// <summary>
/// Level goal. When the player comes to rest (<see cref="St_Pl_Idle"/>) while overlapping this
/// flag, loads <see cref="NextScene"/> directly — self-contained, no GameManager needed.
///
/// The collider is forced to a trigger so the flying player passes straight through (no bounce).
/// Detection is a per-frame geometric overlap (<see cref="Collider2D.Distance"/>) rather than a
/// trigger callback, because a resting player is Kinematic and Unity 2D raises no trigger events
/// for a Kinematic body against a static collider. Add the component and assign Next Scene.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class GoalFlag : MonoBehaviour
{
    [Header("Next Scene")]
    [SerializeField] public SceneReference NextScene;

    private Collider2D _collider;
    private PlayerController _player;
    private bool _reached;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _collider.isTrigger = true; // pass-through, not a wall
    }

    // Author convenience: trigger by default the moment the component is added.
    private void Reset() => GetComponent<Collider2D>().isTrigger = true;

    private void Update()
    {
        if (_reached) return;

        if (_player == null)
        {
            _player = FindAnyObjectByType<PlayerController>();
            if (_player == null) return;
        }

        if (_player.Current is not St_Pl_Idle) return;

        Collider2D playerCollider = _player.Collider != null
            ? _player.Collider
            : _player.GetComponent<Collider2D>();
        if (playerCollider == null) return;

        // Geometric overlap — independent of Rigidbody body types, unlike trigger callbacks or
        // IsTouching, both of which go silent for a Kinematic player against a static collider.
        if (!_collider.Distance(playerCollider).isOverlapped) return;

        _reached = true;

        // Always route through GameManager so CurrentLevelIndex stays in sync.
        // Only fall back to the Inspector-assigned NextScene when no GameManager exists.
        if (GameManager.Instance != null)
        {
            Debug.Log($"[GoalFlag] Reached — calling GameManager.LoadNextLevel (currentIndex={GameManager.Instance.CurrentLevelIndex})");
            GameManager.Instance.LoadNextLevel();
            return;
        }

        if (NextScene != null && !string.IsNullOrEmpty(NextScene.ScenePath))
        {
            Debug.Log($"[GoalFlag] Reached — no GameManager, loading NextScene directly: {NextScene.ScenePath}");
            SceneManager.LoadScene(NextScene.ScenePath);
            return;
        }

        Debug.LogWarning("[GoalFlag] No Next Scene assigned and no GameManager found.", this);
    }
}
