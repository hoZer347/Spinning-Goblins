using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;


namespace hoZer
{
	[RequireComponent(typeof(Rigidbody2D))]
	[RequireComponent(typeof(Collider2D))]
	[RequireComponent(typeof(SpriteRenderer))]
	public class EnemyController : StateMachine<EnemyController>
	{
		[Header("Components")]
		[SerializeField] new public Rigidbody2D		rigidbody;
		[SerializeField] new public Collider2D		collider;
		[SerializeField] public SpriteRenderer		spriteRenderer;
		[HideInInspector] public PlayerController	playerController;

		[Header("Wander Settings")]
		[SerializeField] public float				detectionRadius		= 100f;
		[SerializeField] public float				wanderWaitMin		= 1.0f; 
		[SerializeField] public float				wanderWaitMax		= 3.0f;
		[SerializeField] public float				wanderSpeed			= 1.0f;
		[SerializeField] public float				wanderDistanceMin	= 1.0f;
		[SerializeField] public float				wanderDistanceMax	= 3.0f;
		[SerializeField] public float				wanderTimeMin		= 1.0f;
		[SerializeField] public float				wanderTimeMax		= 3.0f;

		[Header("Hitstun Settings")]
		[SerializeField] public float				hitstunDuration		= 0.5f;
		[SerializeField] public float				stopFriction		= 5f;
		[SerializeField] public float				pauseDuration		= 1.0f;

		[Header("Approach Settings")]
		[SerializeField] public float				approachSpeed		= 40f;

		[Header("Death Settings")]
		[SerializeField] public float				fallingDuration		= 1.0f;

		[Header("Audio Settings")]
		[SerializeField] public AudioSource			audioSource;
		[SerializeField] public AudioClip			hitstunSound;

		// Extra reach added to the look-ahead cast so a hazard is caught a hair before contact.
		const float WallCastSkin = 0.05f;

		private void OnDestroy()
		{
			EnemyController[] enemyController =
				GameObject.FindObjectsByType<EnemyController>(
					FindObjectsSortMode.None);

			if (Application.isPlaying && enemyController.Length == 0)
				SceneManager.LoadScene(FindAnyObjectByType<CutsceneManager>()
					.NextScene);
		}

		private void OnCollisionEnter2D(Collision2D collision)
		{
			if (!(Current is St_En1_Hitstun)) return;

			// gameObject.layer is a layer INDEX; LayerMask.NameToLayer compares indices.
			// Pits are handled by IsFullyInsidePit in OnPhysics, so only Damage / walls here.
			int layer = collision.gameObject.layer;

			if (layer == LayerMask.NameToLayer("Damage"))
				DieFromHazard();             // hit spikes mid-hitstun
			else if (layer == LayerMask.NameToLayer("Obstacle"))
				PlayHit();                   // bounced off a wall mid-hitstun
		}

		// Spike/hazard death: play the impact, then die. The hit is its own line (not in
		// St_En1_Die) because Die can be entered for other reasons that shouldn't sound a hit.
		void DieFromHazard()
		{
			PlayHit();
			SetState<St_En1_Die>();
		}

		// Plays the shared hit sound through the PLAYER's audio source, so it still sounds even
		// when this enemy is being destroyed (which would kill its own AudioSource mid-clip).
		public void PlayHit()
		{
			if (playerController != null && playerController.audioSource != null && playerController.hit != null)
				playerController.audioSource.PlayOneShot(playerController.hit, 0.3f);
		}

		protected override void OnStart()
		{
			// OnValidate only wires these in the editor. Guarantee them at runtime too, so
			// states that touch rigidbody/collider don't NRE on an unwired or runtime-spawned
			// enemy. (PlayerController's references are serialized on its prefab; these aren't.)
			if (rigidbody == null)        rigidbody        = GetComponent<Rigidbody2D>();
			if (collider == null)         collider         = GetComponent<Collider2D>();
			if (spriteRenderer == null)   spriteRenderer   = GetComponent<SpriteRenderer>();
			if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
			if (audioSource == null)	  audioSource = GetComponent<AudioSource>();

			if (rigidbody != null)
			{
				// Top-down game: no gravity. And a Kinematic body only fires OnCollisionEnter2D
				// against the static walls/hazards when full kinematic contacts are enabled —
				// without this, kinematic-vs-static collisions are silent.
				rigidbody.gravityScale = 0f;
				rigidbody.useFullKinematicContacts = true;
			};

			// Slide over Pit tiles instead of bouncing off their solid edge, so the body can get
			// fully inside before it drops in. The IsFullyInsidePit query ignores this exclusion.
			if (collider != null)
				collider.excludeLayers = collider.excludeLayers.value | LayerMask.GetMask("Pits");
		}

		protected override void OnPhysics()
		{
			// Drop into a pit only once the whole collider is inside it — the body slides past the
			// edge first. Runs in any moving state, but not while already dying / falling.
			if (!(Current is St_En1_Falling || Current is St_En1_Die) && IsFullyInsidePit())
			{
				SetState<St_En1_Falling>();
				return;
			};

			// Pre-empt the Damage-wall bounce during the knockback slide (Hitstun / Stopping are
			// the Dynamic phase; Wander/Approach move via transform and carry no velocity here).
			if ((Current is St_En1_Hitstun || Current is St_En1_Stopping) && DamageWallAhead())
				DieFromHazard();
		}

		// True if a Damage wall is within this step's travel, so we react before bouncing off it.
		private bool DamageWallAhead()
		{
			if (rigidbody == null || collider == null)
				return false;

			Vector2 velocity = rigidbody.linearVelocity;
			float   speed    = velocity.magnitude;

			if (speed < 0.01f)
				return false; // not sliding — nothing to pre-empt

			float distance = speed * Time.fixedDeltaTime + WallCastSkin;

			return Physics2D.CircleCast(
				collider.bounds.center,
				collider.bounds.extents.x,
				velocity / speed,
				distance,
				LayerMask.GetMask("Damage")).collider != null;
		}

		protected virtual bool PlayerInRange() =>
			Vector2.Distance(
				transform.position,
				playerController.transform.position) <= detectionRadius;

		// True only when the enemy's entire collider footprint sits over Pit tiles, so it drops in
		// after sliding fully past the edge rather than the instant it brushes one.
		public bool IsFullyInsidePit()
		{
			if (collider == null) return false;

			Bounds b = collider.bounds;

			return OverPit(new Vector2(b.min.x, b.min.y))
				&& OverPit(new Vector2(b.min.x, b.max.y))
				&& OverPit(new Vector2(b.max.x, b.min.y))
				&& OverPit(new Vector2(b.max.x, b.max.y));
		}

		private bool OverPit(Vector2 point) =>
			Physics2D.OverlapPoint(point, LayerMask.GetMask("Pits")) != null;

		// Moves the enemy by `displacement`, but refuses a step that would walk it from clear
		// ground onto a Pit or Damage tile (it can still be knocked onto them). Returns true if
		// it actually moved.
		public bool MoveSafely(Vector2 displacement)
		{
			if (collider == null || displacement == Vector2.zero)
			{
				transform.position += (Vector3)displacement;
				return true;
			};

			int     mask   = LayerMask.GetMask("Pits", "Damage");
			Vector2 center = collider.bounds.center;
			float   radius = collider.bounds.extents.x;

			// Allow the move if we're already on a hazard (so a knocked-back enemy can climb out),
			// but never step onto one from safe ground.
			bool onHazardNow  = Physics2D.OverlapCircle(center, radius, mask) != null;
			bool onHazardNext = Physics2D.OverlapCircle(center + displacement, radius, mask) != null;

			if (!onHazardNow && onHazardNext)
				return false;

			transform.position += (Vector3)displacement;
			return true;
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			if (rigidbody == null)			rigidbody			= GetComponent<Rigidbody2D>();
			if (collider == null)			collider			= GetComponent<Collider2D>();
			if (spriteRenderer == null)		spriteRenderer		= GetComponent<SpriteRenderer>();
			if (playerController == null)	playerController	= GetComponent<PlayerController>();
		}

		void OnDrawGizmos()
		{
			Gizmos.color = new Color(.1f, .0f, .0f, .5f);

			// Detection Radius
			Gizmos.DrawWireSphere(transform.position, detectionRadius);
		}
#endif
	};
};
