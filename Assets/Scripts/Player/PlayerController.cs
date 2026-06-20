using UnityEngine;
using hoZer;


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

	[Header("Movement")]
	[Range(0f, 10f)] public float LinearDamping = 3f;
	[Range(0f, 20f)] public float StickyDamping = 8f;
	[Range(0f, 10f)] public float StickyThreshold = 3f;
	[Range(0f, 1f)] public float Bounciness = 1f;

	[Header("Tilemap Layers")]
	public LayerMask ObstacleLayer; // bounce only, handled entirely by the physics material
	public LayerMask DamageLayer;   // hurts the player on contact
	public LayerMask PitsLayer;     // swallows the player while Idle / Dragging

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

	/// <summary>States during which incoming damage / pits are ignored.</summary>
	public bool IsInvulnerable =>
		Current is St_Pl_IFrames || Current is St_Pl_Falling || Current is St_Pl_Dead;

	protected override void OnStart()
	{
		SpawnPosition = transform.position;
		OriginalScale = transform.localScale;

		Rigidbody.gravityScale = 0f;
		Rigidbody.linearDamping = LinearDamping;

		// Elastic bounce off any solid tile (Obstacle / Damage) with no per-collider setup.
		Rigidbody.sharedMaterial = new PhysicsMaterial2D("PlayerBounce")
		{
			friction = 0f,
			bounciness = Bounciness,
		};

		// Pass straight through Pit tiles instead of bouncing off them. Pits are detected
		// purely by the center-point query in IsCenterOverPit, so we exclude the layer from
		// physical collision. Done in code so it survives scene reserialization and does not
		// depend on the Pit collider's "Is Trigger" flag.
		if (Collider != null)
			Collider.excludeLayers = Collider.excludeLayers.value | PitsLayer.value;

		if (Sprite == null) Sprite = GetComponentInChildren<SpriteRenderer>();
		if (DeathPanel != null) DeathPanel.SetActive(false);

		SetState<St_Pl_Idle>();
	}

	// --- Queries & effects for states to use. No transitions are decided here. ----------

	/// <summary>True when the player's center point lies inside a pit cell.</summary>
	public bool IsCenterOverPit()
	{
		if (Collider == null) return false;
		// Only the player's center point counts, so brushing a pit edge won't drop you in.
		return Physics2D.OverlapPoint(Collider.bounds.center, PitsLayer) != null;
	}

	public bool IsDamageLayer(int layer) => (DamageLayer.value & (1 << layer)) != 0;

	/// <summary>Kicks the camera's own state machine into its shake state, if one exists.</summary>
	public void ShakeCamera()
	{
		CameraController camera = FindAnyObjectByType<CameraController>();
		if (camera != null)
			camera.SetState<St_Cm_Shake>();
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

		// Push the move into the physics world immediately so the next IsCenterOverPit() query
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
