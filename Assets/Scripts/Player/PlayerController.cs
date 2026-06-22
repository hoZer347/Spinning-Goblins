using UnityEngine;
using hoZer;
using Tymski;


public class PlayerController : StateMachine<PlayerController>
{
	[Header("References")]
	public Rigidbody2D Rigidbody;
	public Collider2D Collider;
	public LineRenderer DragLine;
	public SpriteRenderer Sprite;

	[Header("Launch")]
	public float LaunchForceMultiplier = 7f;
	public float MaxDragDistance = 2.5f;
	[Tooltip("How far the body visually stretches relative to the pull (1 = full, lower = more compressed). Launch power is unaffected.")]
	[Range(0.05f, 1f)] public float StretchCompression = 0.5f;

	[Header("Movement")]
	[Range(0f, 10f)] public float LinearDamping = 3f;
	[Range(0f, 20f)] public float StickyDamping = 8f;
	[Range(0f, 10f)] public float StickyThreshold = 3f;
	[Range(0f, 1f)] public float Bounciness = 1f;
	[Tooltip("Speed below which the player snaps immediately to zero.")]
	[Range(0f, 2f)] public float SnapThreshold = 0.4f;

	[Header("Tilemap Layers")]
	public LayerMask ObstacleLayer; // bounce only, handled entirely by the physics material
	public LayerMask DamageLayer;   // hurts the player on contact
	public LayerMask PitsLayer;     // swallows the player while Idle / Dragging
	public LayerMask ExitLayer;     // triggers next-level load on center-point overlap

	[Header("Damage / I-Frames")]
	public float IFrameDuration = 1f;
	public float ScreenShakeDuration = 0.15f;
	public float ScreenShakeMagnitude = 0.15f;

	[Header("Pits")]
	public float FallDuration = 0.5f;

	[Header("Death UI")]
	public GameObject DeathPanel;

	[Header("Stopping")]
	[SerializeField] public float stopFriction = 5f;

	[HideInInspector] public Vector2 SpawnPosition;
	[HideInInspector] public Vector3 OriginalScale;
	[HideInInspector] public Vector2 LaunchForce;
	[HideInInspector] public Vector2 DragClickPosition;

	[Header("Hurt Settings")]
	[SerializeField] public float HurtBounceForce = 20f;
	[SerializeField] public float hurtFlashTime = .1f;
	[SerializeField] public float hitStunTime = 1f;

	[Header("Sounds")]
	[SerializeField] public AudioSource audioSource;
	[SerializeField] public AudioClip hit;
	[SerializeField] public AudioClip stretch;
	[SerializeField] public AudioClip spinWoosh;
	[SerializeField] public AudioClip gobHurt;
	[SerializeField] public AudioClip spin;
	[SerializeField] public AudioClip pitFall;

	[Header("Spin Audio")]
	[SerializeField] float spinVolume = 0.5f;
	[Tooltip("Seconds between spin sounds at lowest intensity (slowest spin rate).")]
	[SerializeField] float spinIntervalMax = 0.4f;
	[Tooltip("Seconds between spin sounds at highest intensity (fastest spin rate).")]
	[SerializeField] float spinIntervalMin = 0.05f;
	[SerializeField] float spinPitchMin = 0.8f;
	[SerializeField] float spinPitchMax = 2.5f;
	[Tooltip("Flight speed (as a fraction of launch power) at/above which the spin stays at full; below it eases off near the end of the flight.")]
	[Range(0.05f, 1f)] [SerializeField] float spinFlightHold = 0.3f;

	float _spinTimer;
	AudioSource spinSource;

	/// <summary>States during which incoming damage / pits are ignored.</summary>
	public bool IsInvulnerable =>
		Current is St_Pl_IFrames || Current is St_Pl_Falling || Current is St_Pl_Dead;

	protected override void OnStart()
	{
		SpawnPosition = transform.position;
		OriginalScale = transform.localScale;

		Rigidbody.gravityScale = 0f;
		Rigidbody.linearDamping = LinearDamping;

		// Register contacts while Kinematic (Idle / Dragging) so the player can be hit by — and
		// collide with — static walls and Damage tiles mid-drag, not only dynamic enemies.
		Rigidbody.useFullKinematicContacts = true;

		// Elastic bounce off any solid tile (Obstacle / Damage) with no per-collider setup.
		Rigidbody.sharedMaterial = new PhysicsMaterial2D("PlayerBounce")
		{
			friction = 0f,
			bounciness = Bounciness,
		};

		// Pass straight through Pit and Exit tiles instead of bouncing off them. Both are
		// detected purely by center-point queries, so we exclude the layers from physical
		// collision. Done in code so it survives scene reserialization.
		if (Collider != null)
			Collider.excludeLayers =
				Collider.excludeLayers.value
				| PitsLayer.value
				| ExitLayer.value;

		if (Sprite == null) Sprite = GetComponentInChildren<SpriteRenderer>();
		if (DeathPanel != null) DeathPanel.SetActive(false);
		if (audioSource == null) audioSource = GetComponent<AudioSource>();

		// Dedicated source for the spin one-shots so their per-spin pitch doesn't affect the
		// other SFX played through audioSource.
		spinSource = gameObject.AddComponent<AudioSource>();
		spinSource.playOnAwake = false;

		SetState<St_Pl_Idle>();
	}

	// --- Queries & effects for states to use. No transitions are decided here. ----------

	/// <summary>True only when the player's entire collider footprint sits over pit cells, so it
	/// drops in after sliding fully past the edge rather than the instant it brushes one.</summary>
	public bool IsFullyInsidePit()
	{
		if (Collider == null) return false;

		Bounds b = Collider.bounds;

		return OverPit(new Vector2(b.min.x, b.min.y))
			&& OverPit(new Vector2(b.min.x, b.max.y))
			&& OverPit(new Vector2(b.max.x, b.min.y))
			&& OverPit(new Vector2(b.max.x, b.max.y));
	}

	private bool OverPit(Vector2 point) => Physics2D.OverlapPoint(point, PitsLayer) != null;

	/// <summary>True when the player's center point lies inside an exit zone cell.</summary>
	public bool IsCenterOverExit()
	{
		if (Collider == null) return false;
		return Physics2D.OverlapPoint(Collider.bounds.center, ExitLayer) != null;
	}

	public bool IsDamageLayer(int layer) => (DamageLayer.value & (1 << layer)) != 0;

	public bool IsObstacleLayer(int layer) => (ObstacleLayer.value & (1 << layer)) != 0;

	protected override void OnUpdate()
	{
		if (!IsInvulnerable && IsCenterOverExit())
			GameManager.Instance?.RestartLevel();

		if (GameObject.FindAnyObjectByType<EnemyController>() == null)
			GameManager.Instance?.LoadNextLevel();
	}

	/// <summary>Kicks the camera's own state machine into its shake state, if one exists.</summary>
	public void ShakeCamera()
	{
		CameraController camera = FindAnyObjectByType<CameraController>();
		if (camera != null)
			camera.SetState<St_Cm_Shake>();
	}

	// --- Spin audio ---------------------------------------------------------------------

	/// <summary>Resets the spin cadence so the next SpinTick fires promptly.</summary>
	public void ResetSpin() => _spinTimer = 0f;

	/// <summary>
	/// Plays the spin whoosh on a repeating interval whose RATE rises with intensity — launch
	/// power while dragging, flight speed while flying. Higher intensity = more spins per second.
	/// </summary>
	/// <summary>Launch power at full stretch — also the rough top flight speed. Spin intensity is
	/// normalised against this so pitch / rate peak exactly at the stretch limit.</summary>
	public float MaxLaunchPower => MaxDragDistance * LaunchForceMultiplier;

	/// <summary>Drag / braking spin: linear with intensity, peaking at full launch power.</summary>
	public void SpinTick(float intensity)
		=> Spin(MaxLaunchPower > 0f ? Mathf.Clamp01(intensity / MaxLaunchPower) : 0f);

	/// <summary>Flight spin: stays at full while moving fast, easing off only once the speed drops
	/// below spinFlightHold of the launch power — high at the start, dropping near the end.</summary>
	public void SpinFlightTick(float speed)
	{
		float reference = MaxLaunchPower * spinFlightHold;
		Spin(reference > 0f ? Mathf.Clamp01(speed / reference) : 0f);
	}

	// Paces and plays one spin whoosh from an already-normalised intensity (0..1).
	void Spin(float t)
	{
		_spinTimer -= Time.deltaTime;
		if (_spinTimer > 0f) return;

		// Both the pitch of each whoosh and the gap until the next rise with intensity.
		if (spin != null && spinSource != null)
		{
			spinSource.pitch = Mathf.Lerp(spinPitchMin, spinPitchMax, t);
			spinSource.PlayOneShot(spin, spinVolume);
		}

		_spinTimer = Mathf.Lerp(spinIntervalMax, spinIntervalMin, t);
	}

	/// <summary>Resets the player to its starting position, scale, rotation and a clean velocity.</summary>
	public void RespawnAtStart()
	{
		Rigidbody.linearVelocity = Vector2.zero;
		Rigidbody.angularVelocity = 0f;
		Rigidbody.position = SpawnPosition;
		Rigidbody.rotation = 0f;
		transform.position = SpawnPosition;
		transform.localScale = OriginalScale;
		transform.rotation = Quaternion.identity;

		// Push the move into the physics world immediately so the next IsFullyInsidePit() query
		// reads the new position, instead of the stale (pit) location it held pre-respawn.
		Physics2D.SyncTransforms();
	}

	/// <summary>The active state narrowed to the player base. Every player state derives from
	/// St_Pl_Base, so this is the single place the machine's generic State gets cast.</summary>
	private St_Pl_Base ActiveState => Current as St_Pl_Base;

	/// <summary>
	/// External damage entry point (e.g. enemies). Routed to the current state so the
	/// transition decision stays inside a state, never on the machine.
	/// </summary>
	public void TakeDamage() => ActiveState?.OnDamage();

	// --- MonoBehaviour messages: forward to the current state, decide nothing here. ------

	private void OnCollisionEnter2D(Collision2D col) => ActiveState?.OnContact(col.collider);
	private void OnTriggerEnter2D(Collider2D col) => ActiveState?.OnContact(col);
}
