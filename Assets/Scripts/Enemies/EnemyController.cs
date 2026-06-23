using UnityEngine;
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
		[SerializeField] public float				hitstunKnockback	= 18f;
		[SerializeField] public float				stopFriction		= 5f;
		[SerializeField] public float				pauseDuration		= 1.0f;

		[Header("Approach Settings")]
		[SerializeField] public float				approachSpeed		= 40f;

		[Header("Fling (optional)")]
		[Tooltip("Enables the stretch-and-fling lunge. Off = plain basic enemy.")]
		[SerializeField] public bool				canFling			= false;
		[SerializeField] public float				flingRange			= 4.0f;
		[SerializeField] public float				flingWindup			= 0.6f;
		[Tooltip("Lunge speed. Keep below the player's launch power so it stays beatable.")]
		[SerializeField] public float				flingSpeed			= 12f;
		[Tooltip("Body stretch factor during the wind-up telegraph.")]
		[SerializeField] public float				flingStretch		= 1.5f;
		[Tooltip("Seconds of chasing between lunges.")]
		[SerializeField] public float				flingCooldown		= 1.5f;

		[HideInInspector] public Vector2			flingDirection;
		[HideInInspector] public float				flingReadyAt;

		[Header("Death Settings")]
		[SerializeField] public float				fallingDuration		= 1.0f;

 		[Header("Health")]
		[Tooltip("How much of the enemy budget this prefab costs when spawned.")]
		[SerializeField] public float				spawnCost			= 1f;
		[SerializeField] public int					maxHealth			= 3;
		[SerializeField] public float				damageCooldown		= 0.25f;
		[SerializeField] public float				healthBarHeight		= 0.6f;

		int				health;
		float			damageReadyAt;
		EnemyHealthBar	healthBar;

		/// <summary>Remaining hit points — read by pit-kill scoring before the enemy drops in.</summary>
		public int Health => health;

		[Header("Audio Settings")]
		[SerializeField] public AudioSource			audioSource;
		[SerializeField] public AudioClip			hitstunSound;
		[SerializeField] public AudioClip			pitFall;

		[Header("Sprites")]
		[SerializeField] public Sprite				sprIdle;
		[SerializeField] public Sprite				sprWalkUp;
		[SerializeField] public Sprite				sprWalkLeft;
		[SerializeField] public Sprite				sprWalkDown;
		[SerializeField] public Sprite				sprWalkRight;
		[SerializeField] public Sprite				sprSleeping;
		[SerializeField] public Sprite				sprSpinning;
		[SerializeField] public Sprite				sprFalling;

		// Extra reach added to the look-ahead cast so a hazard is caught a hair before contact.
		const float WallCastSkin = 0.05f;

		private void OnDestroy()
		{
			if (!Application.isPlaying) return;

			EnemyController[] enemyController =
				GameObject.FindObjectsByType<EnemyController>(
					FindObjectsSortMode.None);

			if (enemyController.Length != 0) return;

			// Route through GameManager so the swipe transition fires and CurrentLevelIndex stays in sync.
			if (GameManager.Instance != null)
			{
				Debug.Log("[EnemyController] Last enemy destroyed — calling GameManager.LoadNextLevel()");
				GameManager.Instance.LoadNextLevel();
			}
		}

		private void OnCollisionEnter2D(Collision2D collision)
		{
			if (!(Current is St_En1_Hitstun)) return;

			// gameObject.layer is a layer INDEX; LayerMask.NameToLayer compares indices.
			// Pits are handled by IsFullyInsidePit in OnPhysics, so only Damage / walls here.
			int layer = collision.gameObject.layer;

			if (layer == LayerMask.NameToLayer("Damage"))
				HurtByHazard();              // hit spikes mid-hitstun
			else if (layer == LayerMask.NameToLayer("Obstacle"))
				PlayHit();                   // bounced off a wall mid-hitstun

			// Knocked into another enemy: pass the hit on. It takes damage + hitstun and, carried by
			// the same collision impulse, can knock into the next enemy — a chain reaction.
			EnemyController other = collision.gameObject.GetComponent<EnemyController>();
			if (other != null)
				other.HitByEnemy();
		}

		// Spike/hazard contact: play the impact and spend one health dot. The damage cooldown lives
		// HERE (hazard-only) so sliding along spikes coalesces into single hits, while a player hit
		// never starts the cooldown — and therefore never blocks the spike it knocks the enemy into.
		void HurtByHazard()
		{
			if (Time.time < damageReadyAt) return;
			damageReadyAt = Time.time + damageCooldown;

			PlayHit();
			ScoreUI.Instance?.AddHazardDamage(1); // 20 per point of damage dealt
			TakeDamage();
		}

		/// <summary>
		/// A knocked enemy slammed into us — take a hit and join the chain. Guarded so an enemy
		/// already reeling / dying isn't re-hit: two knocked enemies can't ping-pong damage forever,
		/// and each fresh enemy the chain reaches is hit exactly once. Shares the hazard cooldown.
		/// </summary>
		public void HitByEnemy()
		{
			if (Current is St_En1_Hitstun || Current is St_En1_Die || Current is St_En1_Falling) return;
			if (Time.time < damageReadyAt) return;
			damageReadyAt = Time.time + damageCooldown;

			PlayHit();
			TakeDamage();
		}

		/// <summary>
		/// Spends one health dot. Surviving a hit re-enters hitstun; reaching zero dies. The hazard
		/// coalescing cooldown is applied by the caller (HurtByHazard), not here, so player hits and
		/// the spikes they cause both land.
		/// </summary>
		public void TakeDamage(int amount = 1)
		{
			if (Current is St_En1_Die || Current is St_En1_Falling) return;

			health = Mathf.Max(0, health - amount);
			if (healthBar != null) healthBar.SetHealth(health);

			if (health <= 0)
				SetState<St_En1_Die>();
			else
				SetState<St_En1_Hitstun>();
		}

		// Plays the shared hit clip (it lives on the player) on a throwaway source via
		// PlayClipAtPoint, so it (a) survives this enemy being destroyed, and (b) isn't masked by
		// the player's own source, which is busy machine-gunning spin whooshes during flight.
		// Played at the camera/listener so it stays full 2D volume wherever the enemy is.
		public void PlayHit()
		{
			AudioClip clip = playerController != null ? playerController.hit : null;
			if (clip == null) return;

			Vector3 at = Camera.main != null ? Camera.main.transform.position : transform.position;
			AudioSource.PlayClipAtPoint(clip, at, 0.6f);
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

				// Stay upright — knockback bounces never spin the body (the spin is a sprite anim now).
				rigidbody.freezeRotation = true;
			};

			// Slide over Pit tiles instead of bouncing off their solid edge, so the body can get
			// fully inside before it drops in. The IsFullyInsidePit query ignores this exclusion.
			if (collider != null)
				collider.excludeLayers = collider.excludeLayers.value | LayerMask.GetMask("Pits");

			health    = maxHealth;
			healthBar = EnemyHealthBar.Create(transform, maxHealth, healthBarHeight);
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
			if ((Current is St_En1_Hitstun || Current is St_En1_Stopping || Current is St_En1_Fling) && DamageWallAhead())
				HurtByHazard();
		}

		// True if a collider on `mask` is within this step's travel along the current velocity.
		private bool Ahead(int mask)
		{
			if (rigidbody == null || collider == null)
				return false;

			Vector2 velocity = rigidbody.linearVelocity;
			float   speed    = velocity.magnitude;

			if (speed < 0.01f)
				return false; // not moving — nothing to pre-empt

			float distance = speed * Time.fixedDeltaTime + WallCastSkin;

			return Physics2D.CircleCast(
				collider.bounds.center,
				collider.bounds.extents.x,
				velocity / speed,
				distance,
				mask).collider != null;
		}

		// A Damage wall ahead — react before bouncing off it.
		private bool DamageWallAhead() => Ahead(LayerMask.GetMask("Damage"));

		// A pit ahead — used to stop a self-propelled lunge before it carries the enemy into a pit
		// by itself (it can still be knocked in, since that's not driven by this check).
		public bool PitAhead() => Ahead(LayerMask.GetMask("Pits"));

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
