using UnityEngine;

/// <summary>
/// Periodically spawns enemies from a weighted set of prefabs, somewhere inside a box around this
/// object — never on the Damage, Pits, or Obstacle layers.
///
/// Each entry can carry a spawn cost (drawn from the spawner's running budget) and a time restriction
/// (only spawnable after a number of seconds), so waves can ramp up: cheap enemies from the start,
/// pricier ones unlocking later, until the budget runs dry. The first time an entry's time threshold
/// is met it gets one GUARANTEED spawn (its "debut") even if the budget can't afford it — so a unit
/// always shows up when it unlocks; budget rules apply to it only from its second spawn on.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
	[System.Serializable]
	public class Entry
	{
		public GameObject prefab;
		[Min(0f)] public float weight = 1f;
		[Tooltip("Subtracted from the spawner's running budget each time this enemy spawns.")]
		[Min(0f)] public float cost = 0f;
		[Tooltip("This enemy can only spawn once this many seconds have elapsed. 0 = available from the start.")]
		[Min(0f)] public float availableAfter = 0f;

		// Runtime only: set true once this entry has had its guaranteed first spawn (its "debut").
		[System.NonSerialized] public bool hasDebuted;
	}

	[Header("Enemies")]
	[Tooltip("Spawnable enemy prefabs with relative probability weights.")]
	[SerializeField] Entry[] enemies;

	[Header("Budget")]
	[Tooltip("Running total the spawner may spend. Each spawn subtracts its enemy's cost; once nothing " +
		"affordable remains, spawning stops. 0 = unlimited (costs are ignored).")]
	[SerializeField] float budget = 0f;

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
	float _elapsed;          // seconds this spawner has been active — the clock for per-entry availableAfter
	float _remainingBudget;
	Transform _player;

	// Prefer the shared level clock (TimeUI) so per-entry availableAfter unlocks line up with the timer
	// the player sees; fall back to our own accumulator in scenes without a TimeUI.
	private float ElapsedTime => TimeUI.Instance != null ? TimeUI.Instance.TimeElapsed : _elapsed;

	private void Start()
	{
		_blockedLayers = LayerMask.GetMask("Damage", "Pits", "Obstacle");
		_timer = spawnOnStart ? spawnInterval : 0f;
		_remainingBudget = budget;
	}

	private void Update()
	{
		_elapsed += Time.deltaTime;

		_timer += Time.deltaTime;
		if (_timer < spawnInterval) return;
		_timer = 0f;

		// A first-time cutscene dwarf is a boss moment: nothing else spawns while it's alive.
		if (FirstTimeCutsceneTrigger.SpawningBlocked) return;

		if (maxAlive > 0 && AliveCount() >= maxAlive) return;

		// A newly unlocked unit gets a guaranteed first spawn (its debut) even if the budget can't afford
		// it; otherwise fall back to the normal weighted, budget-gated pick.
		Entry entry = PickDebut() ?? PickEligible();
		if (entry == null) return;

		if (TryFindSpawnPoint(out Vector2 pos))
		{
			Instantiate(entry.prefab, pos, Quaternion.identity);
			entry.hasDebuted = true;
			// Spend the cost, clamped so a forced (over-budget) debut can't drive the budget negative.
			if (budget > 0f) _remainingBudget = Mathf.Max(0f, _remainingBudget - entry.cost);
		}
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

	// Weighted pick among the entries that are currently eligible: a real prefab with positive weight,
	// already unlocked by its time restriction, and affordable on the remaining budget.
	private Entry PickEligible()
	{
		if (enemies == null || enemies.Length == 0) return null;

		float total = 0f;
		foreach (Entry e in enemies)
			if (Eligible(e)) total += Mathf.Max(0f, e.weight);

		if (total <= 0f) return null;

		float r = Random.value * total;
		foreach (Entry e in enemies)
		{
			if (!Eligible(e)) continue;
			r -= Mathf.Max(0f, e.weight);
			if (r <= 0f) return e;
		}
		return null;
	}

	// The first entry that has just become available (its time threshold is met) but hasn't had its
	// guaranteed debut spawn yet — it spawns next regardless of budget. Weight is deliberately ignored
	// here: a one-time boss can sit at weight 0 (never randomly rolled) and still debut on schedule.
	// Null once every unlocked entry has debuted.
	private Entry PickDebut()
	{
		if (enemies == null) return null;

		foreach (Entry e in enemies)
			if (e != null && e.prefab != null && ElapsedTime >= e.availableAfter && !e.hasDebuted)
				return e;

		return null;
	}

	// A usable entry: a real prefab with a positive weight.
	private bool Valid(Entry e) => e != null && e.prefab != null && Mathf.Max(0f, e.weight) > 0f;

	private bool Eligible(Entry e) =>
		Valid(e)
		&& ElapsedTime >= e.availableAfter              // time restriction: only after availableAfter
		&& (budget <= 0f || e.cost <= _remainingBudget); // affordable (or budget disabled)

	// Live enemy count from EnemyController's running tally: it bumps up on spawn and back down the
	// instant an enemy dies (Death/Falling) — reliable where the old scene scan lagged a corpse's
	// animation or kept counting a body that never finished dying.
	private int AliveCount() => hoZer.EnemyController.AliveCount;

#if UNITY_EDITOR
	private void OnDrawGizmosSelected()
	{
		Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.6f);
		Gizmos.DrawWireCube(transform.position, new Vector3(spawnArea.x, spawnArea.y, 0f));
	}
#endif
}
