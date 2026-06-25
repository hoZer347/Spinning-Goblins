using UnityEngine;

/// <summary>
/// Periodically spawns enemies from a weighted set of prefabs, somewhere inside a box around this
/// object — never on the Damage, Pits, or Obstacle layers.
///
/// Each entry can carry a spawn cost and a time restriction (only spawnable after a number of seconds),
/// so waves can ramp up: cheap enemies from the start, pricier ones unlocking later. The budget caps the
/// total cost of enemies ALIVE AT ONCE — it's refunded as they die, so the arena keeps refilling rather
/// than running dry. The first time an entry's time threshold is met it gets one GUARANTEED spawn (its
/// "debut") even if it'd exceed the budget; budget rules apply to it from its second spawn on.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
	[System.Serializable]
	public class Entry
	{
		public GameObject prefab;
		[Min(0f)] public float weight = 1f;
		[Tooltip("This enemy's cost against the budget while it's alive (refunded when it dies).")]
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
	[Tooltip("Cap on the total cost of enemies ALIVE AT ONCE. Each living enemy holds its cost; the cost " +
		"is refunded the instant it dies, so spawning resumes as the player clears the arena. " +
		"0 = unlimited (costs ignored; only Max Alive caps the population).")]
	[SerializeField] float budget = 0f;

	[Header("Health Pickups")]
	[Tooltip("Hard cap on how many health pickups can be alive at once — counted separately from the " +
		"enemy budget / alive-cap. 0 = unlimited. A pickup entry is any prefab in the list with a " +
		"HealthPickup component.")]
	[SerializeField] int maxHealthPickupsAlive = 2;

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
	Transform _player;
	int _livePickups;        // pickups actually alive, recounted each spawn tick so the cap can't drift

	// Prefer the shared level clock (TimeUI) so per-entry availableAfter unlocks line up with the timer
	// the player sees; fall back to our own accumulator in scenes without a TimeUI.
	private float ElapsedTime => TimeUI.Instance != null ? TimeUI.Instance.TimeElapsed : _elapsed;

	private void Start()
	{
		_blockedLayers = LayerMask.GetMask("Damage", "Pits", "Obstacle");
		_timer = spawnOnStart ? spawnInterval : 0f;
	}

	private void Update()
	{
		_elapsed += Time.deltaTime;

		_timer += Time.deltaTime;
		if (_timer < spawnInterval) return;
		_timer = 0f;

		// A first-time cutscene dwarf is a boss moment: nothing else spawns while it's alive.
		if (FirstTimeCutsceneTrigger.SpawningBlocked) return;

		// Count the health pickups that are ACTUALLY alive right now (cheap — once per spawn tick), so the
		// cap always reflects reality. A hand-kept +/- tally drifts: a missed decrement (a destroy that
		// skipped OnDestroy, a scene change) leaves it stuck at the cap and pickups never return.
		_livePickups = maxHealthPickupsAlive > 0
			? FindObjectsByType<HealthPickup>(FindObjectsSortMode.None).Length
			: 0;

		// A newly unlocked unit gets a GUARANTEED first spawn (its debut) regardless of the budget AND the
		// enemy alive-cap — so the Beeg Dwarf's cutscene entrance can't be suppressed by a full arena. The
		// normal weighted pick applies the per-kind caps itself in Eligible (enemies: budget + alive-cap;
		// health pickups: their own separate count cap).
		Entry entry = PickDebut() ?? PickEligible();
		if (entry == null) return;

		if (TryFindSpawnPoint(out Vector2 pos))
		{
			Instantiate(entry.prefab, pos, Quaternion.identity);
			entry.hasDebuted = true;
			// No manual spend: the spawned enemy adds its spawnCost to EnemyController.AliveCost itself,
			// and refunds it when it dies — so the budget tracks what's alive, not a one-way total.
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
			if (e != null && e.prefab != null && ElapsedTime >= e.availableAfter && !e.hasDebuted && !PickupAtCap(e))
				return e;

		return null;
	}

	// A usable entry: a real prefab with a positive weight.
	private bool Valid(Entry e) => e != null && e.prefab != null && Mathf.Max(0f, e.weight) > 0f;

	private bool Eligible(Entry e)
	{
		if (!Valid(e) || ElapsedTime < e.availableAfter) return false; // unlocked by its timer

		// Health pickups answer ONLY to their own separate count cap — never the enemy budget or alive-cap.
		if (IsPickup(e))
			return !PickupAtCap(e);

		// Enemies: capped by the live population and the live-cost budget.
		return (maxAlive <= 0 || AliveCount() < maxAlive)
			&& (budget <= 0f || hoZer.EnemyController.AliveCost + CostOf(e) <= budget);
	}

	// A prefab carrying a HealthPickup is a "pickup" entry, capped separately from enemies.
	private static bool IsPickup(Entry e) => e.prefab != null && e.prefab.GetComponent<HealthPickup>() != null;

	// True when this entry is a health pickup and its separate cap is already full (using the live count
	// recounted at the top of this tick).
	private bool PickupAtCap(Entry e) =>
		maxHealthPickupsAlive > 0 && IsPickup(e) && _livePickups >= maxHealthPickupsAlive;

	// The cost this entry adds to AliveCost when it spawns — read straight off the prefab's EnemyController
	// so the eligibility check matches exactly what the spawned enemy contributes (falls back to Entry.cost
	// for a prefab without an EnemyController).
	private float CostOf(Entry e)
	{
		hoZer.EnemyController ec = e.prefab != null ? e.prefab.GetComponent<hoZer.EnemyController>() : null;
		return ec != null ? ec.spawnCost : e.cost;
	}

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
