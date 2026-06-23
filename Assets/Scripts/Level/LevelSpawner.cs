using System.Collections.Generic;
using UnityEngine;
using hoZer;


/// <summary>
/// Place in each endless map scene. Reads the current enemy budget from GameManager
/// and spawns enemies from the pool at the scene's spawn points.
/// Does nothing in tutorial mode.
/// </summary>
public class LevelSpawner : MonoBehaviour
{
    [Tooltip("Leave empty to use the pool defined on GameManager.")]
    public EnemyController[] EnemyPoolOverride;
    public Transform[]       SpawnPoints;

    private void Start()
    {
        if (GameManager.Instance == null)      { Debug.LogWarning("[LevelSpawner] No GameManager found.");         return; }
        if (!GameManager.Instance.IsEndlessMode) { Debug.Log("[LevelSpawner] Not in endless mode — skipping spawn."); return; }
        SpawnEnemies(GameManager.Instance.CurrentEnemyBudget);
    }

    private void SpawnEnemies(float budget)
    {
        EnemyController[] pool = (EnemyPoolOverride != null && EnemyPoolOverride.Length > 0)
            ? EnemyPoolOverride
            : GameManager.Instance?.EnemyPool;

        Debug.Log($"[LevelSpawner] budget={budget}, pool={pool?.Length ?? 0}, points={SpawnPoints?.Length ?? 0}");

        if (SpawnPoints == null || SpawnPoints.Length == 0) { Debug.LogWarning("[LevelSpawner] No SpawnPoints assigned.");      return; }
        if (pool        == null || pool.Length        == 0) { Debug.LogWarning("[LevelSpawner] No enemies in pool (set EnemyPool on GameManager or EnemyPoolOverride here)."); return; }

        List<Transform> points = new List<Transform>(SpawnPoints);
        for (int i = points.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (points[i], points[j]) = (points[j], points[i]);
        }

        float remaining = budget;
        int   pi        = 0;

        while (remaining > 0f && pi < points.Count)
        {
            var options = new List<EnemyController>();
            foreach (var e in pool)
                if (e != null && e.spawnCost <= remaining)
                    options.Add(e);

            if (options.Count == 0) break;

            EnemyController choice = options[Random.Range(0, options.Count)];
            Instantiate(choice, points[pi].position, Quaternion.identity);
            remaining -= choice.spawnCost;
            pi++;
        }
    }
}
