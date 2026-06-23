using UnityEngine;

/// <summary>
/// Periodically spawns enemies from a weighted set of prefabs, somewhere inside a box around this
/// object — never on the Damage, Pits, or Obstacle layers.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
	[System.Serializable]
	public class Entry
	{
		public GameObject prefab;
		[Min(0f)] public float weight = 1f;
	}

	[Header("Enemies")]
	[Tooltip("Spawnable enemy prefabs with relative probability weights.")]
	[SerializeField] Entry[] enemies;

	[Header("Spawning")]
	[Tooltip("Seconds between spawns.")]
	[SerializeField] float spawnInterval = 2f;
	[Tooltip("0 = no cap. Otherwise pauses spawning while at least this many enemies are alive.")]
	[SerializeField] int maxAlive = 0;
	[Tooltip("Spawn one immediately on start.")]
	[SerializeField] bool spawnOnStart = true;

	[Header("Placement")]
	[Tooltip("Enemies spawn at a random point inside this box (full width/height), centered here.")]
	[SerializeField] Vector2 spawnArea = new Vector2(6f, 6f);
	[Tooltip("Reject spawn points closer than this to the player, so enemies appear well away from them.")]
	[SerializeField] float minPlayerDistance = 12f;
	[Tooltip("Required clear radius — a candidate point is rejected if anything on the blocked layers is within it.")]
	[SerializeField] float clearRadius = 0.5f;
	[Tooltip("How many random points to try before giving up on a spawn this tick.")]
	[SerializeField] int placementAttempts = 30;

	// Enemies must never spawn on these. Resolved once; names match the rest of the project.
	int _blockedLayers;
	float _timer;
	Transform _player;

	private void Start()
	{
		_blockedLayers = LayerMask.GetMask("Damage", "Pits", "Obstacle");
		_timer = spawnOnStart ? spawnInterval : 0f;
	}

	private void Update()
	{
		_timer += Time.deltaTime;
		if (_timer < spawnInterval) return;
		_timer = 0f;

		if (maxAlive > 0 && AliveCount() >= maxAlive) return;

		GameObject prefab = PickWeighted();
		if (prefab == null) return;

		if (TryFindSpawnPoint(out Vector2 pos))
			Instantiate(prefab, pos, Quaternion.identity);
	}

	private bool TryFindSpawnPoint(out Vector2 point)
	{
		if (_player == null)
		{
			PlayerController pc = FindAnyObjectByType<PlayerController>();
			if (pc != null) _player = pc.transform;
		}

		for (int i = 0; i < placementAttempts; i++)
		{
			Vector2 p = (Vector2)transform.position + new Vector2(
				Random.Range(-spawnArea.x, spawnArea.x) * 0.5f,
				Random.Range(-spawnArea.y, spawnArea.y) * 0.5f);

			// Keep enemies well clear of the player.
			if (_player != null && Vector2.Distance(p, _player.position) < minPlayerDistance)
				continue;

			if (Physics2D.OverlapCircle(p, clearRadius, _blockedLayers) == null)
			{
				point = p;
				return true;
			}
		}

		point = default;
		return false; // nothing far enough / clear enough this tick — skip the spawn
	}

	private GameObject PickWeighted()
	{
		if (enemies == null || enemies.Length == 0) return null;

		float total = 0f;
		foreach (Entry e in enemies)
			if (e != null && e.prefab != null) total += Mathf.Max(0f, e.weight);

		if (total <= 0f) return null;

		float r = Random.value * total;
		foreach (Entry e in enemies)
		{
			if (e == null || e.prefab == null) continue;
			r -= Mathf.Max(0f, e.weight);
			if (r <= 0f) return e.prefab;
		}
		return null;
	}

	private int AliveCount() =>
		FindObjectsByType<hoZer.EnemyController>(FindObjectsSortMode.None).Length;

#if UNITY_EDITOR
	private void OnDrawGizmosSelected()
	{
		Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.6f);
		Gizmos.DrawWireCube(transform.position, new Vector3(spawnArea.x, spawnArea.y, 0f));
	}
#endif
}
