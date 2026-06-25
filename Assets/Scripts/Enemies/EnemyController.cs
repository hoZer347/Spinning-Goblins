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

		[Header("Spin Attack (Beeg Dwarf)")]
		[Tooltip("Enables the charge-up spin attack (the Beeg Dwarf). Off = no spin attack.")]
		[SerializeField] public bool				canSpinAttack		= false;
		[Tooltip("Distance at which the dwarf detects the player and begins charging a spin — it does not " +
			"need to close in first. Keep it well above the dwarf+player collider radii (the Beeg is 2x " +
			"scale, so above ~2.7) or it bumps and shoves the player before it can charge.")]
		[SerializeField] public float				spinDetectRange		= 8.0f;
		[Tooltip("Seconds spent charging the spin. The dwarf is vulnerable (hittable into hitstun) the whole time.")]
		[SerializeField] public float				spinWindup			= 1.0f;
		[Tooltip("Seconds the spin attack itself lasts (~1s).")]
		[SerializeField] public float				spinAttackTime		= 1.0f;
		[Tooltip("World distance the spin glides — roughly one dwarf body.")]
		[SerializeField] public float				spinAttackDistance	= 2.0f;
		[Tooltip("Seconds of chasing between spin attacks.")]
		[SerializeField] public float				spinCooldown		= 1.5f;
		[Tooltip("Peak squash/stretch of the spin wobble (0 = none).")]
		[SerializeField] public float				spinSquash			= 0.3f;
		[Tooltip("Number of squash/stretch wobble cycles across a single spin.")]
		[SerializeField] public float				spinWobbleCycles	= 4f;
		[Tooltip("Seconds spent slowing to a stop — and winding the spin sound down in pitch and rate — after the spin.")]
		[SerializeField] public float				spinRecoverTime		= 0.8f;
		[Tooltip("Recoil speed kicked into the dwarf when a player strikes it mid-spin.")]
		[SerializeField] public float				spinHitBounceSpeed	= 14f;

		[HideInInspector] public Vector2			spinDirection;
		[HideInInspector] public float				spinReadyAt;

		// 0..1 wind-up of the cutscene spin (St_En1_CutsceneSpin), read by EnemySpriteAnimator so the
		// whirl visibly speeds up. Zero at all other times.
		[HideInInspector] public float				cutsceneSpinIntensity;

		// The body's natural (rest) scale, cached at startup so the spin states can grow from and shrink
		// back to it cleanly across the SpinAttack -> SpinRecover handoff.
		[HideInInspector] public Vector3			restScale			= Vector3.one;

		// Decaying recoil applied while spinning when a player strikes the dwarf — the "little bounce".
		[HideInInspector] public Vector2			spinBounceVelocity;

		[Header("Death Settings")]
		[SerializeField] public float				fallingDuration		= 1.0f;

 		[Header("Health")]
		[Tooltip("How much of the enemy budget this prefab costs when spawned.")]
		[SerializeField] public float				spawnCost			= 1f;
		[SerializeField] public int					maxHealth			= 3;
		[Tooltip("Score for killing this enemy. A pit kill adds 20 per remaining HP on top of this.")]
		[SerializeField] public int					killScore			= 100;
		[SerializeField] public float				damageCooldown		= 0.25f;
		[SerializeField] public float				healthBarHeight		= 0.6f;

		int				health;
		float			damageReadyAt;
		Vector2			_prevCenter;     // collider centre last physics step, for the swept pit-crossing test
		EnemyHealthBar	healthBar;

		/// <summary>Remaining hit points — read by pit-kill scoring before the enemy drops in.</summary>
		public int Health => health;

		/// <summary>
		/// Live enemy tally, kept as a running counter rather than a scene scan: bumped up when an enemy
		/// starts (OnStart) and back down the instant it dies — St_En1_Death / St_En1_Falling call
		/// <see cref="StopCounting"/> — with OnDestroy as a backstop for any other exit. The spawner reads
		/// this for its alive cap. Static, so it spans every spawner the way the old scene scan did.
		/// </summary>
		public static int AliveCount { get; private set; }

		/// <summary>
		/// Summed <see cref="spawnCost"/> of every live enemy. The spawner's budget caps THIS — the cost
		/// of what's alive right now — refunded the instant an enemy dies, so the spawner keeps refilling
		/// the arena instead of spending a one-way total down to zero and never spawning again.
		/// </summary>
		public static float AliveCost { get; private set; }

		bool  _countedAlive;
		float _countedCost;   // the spawnCost we added to AliveCost, refunded exactly once

		/// <summary>
		/// Drops this enemy out of <see cref="AliveCount"/> / <see cref="AliveCost"/> exactly once — called
		/// the moment it dies (entering Death/Falling) so a kill frees a spawn slot and budget immediately,
		/// with OnDestroy as a backstop for any other exit (cutscene knock-off, scene unload). The flag
		/// prevents a double count.
		/// </summary>
		public void StopCounting()
		{
			if (!_countedAlive) return;
			_countedAlive = false;
			AliveCount = Mathf.Max(0, AliveCount - 1);
			AliveCost  = Mathf.Max(0f, AliveCost - _countedCost);
		}

		// Zero the static tallies at the start of every play session, even with editor Domain Reload off.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void ResetAliveTallies()
		{
			AliveCount = 0;
			AliveCost  = 0f;
		}

		[Header("Audio Settings")]
		[SerializeField] public AudioSource			audioSource;
		[SerializeField] public AudioClip			hitstunSound;
		[SerializeField] public AudioClip			pitFall;

		[Header("Spin Audio (Beeg Dwarf)")]
		[Tooltip("Spin whoosh clip. Falls back to the player's spin clip if left unset.")]
		[SerializeField] public AudioClip			spinSound;
		[SerializeField] public float				spinVolume			= 0.5f;
		[Tooltip("Spin pitch at the start of the charge — kept well below the player's so it reads as a deeper, heavier spin.")]
		[SerializeField] public float				spinPitchLow		= 0.3f;
		[Tooltip("Spin pitch at full charge / mid-attack. Still lower than the player's spin.")]
		[SerializeField] public float				spinPitchHigh		= 0.7f;
		[Tooltip("Seconds between spin whooshes at the start of the charge (low frequency).")]
		[SerializeField] public float				spinIntervalMax		= 0.45f;
		[Tooltip("Seconds between spin whooshes at full charge (higher frequency).")]
		[SerializeField] public float				spinIntervalMin		= 0.12f;

		float				_spinSoundTimer;
		[HideInInspector] public AudioSource spinAudioSource;

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

		// NOTE: clearing the last enemy no longer auto-advances the level. A scene transitions on "no
		// enemies left" ONLY when an AllEnemyDeadSceneTrans behaviour is present to drive it — that
		// component owns the decision now, so levels without it never end just because the arena emptied.

		private void OnCollisionEnter2D(Collision2D collision)
		{
			// Both the reeling slide (Hitstun) and the dead skid (Death) are moving-projectile phases
			// that can knock into things; in any other state we ignore collisions.
			bool sliding = Current is St_En1_Hitstun || Current is St_En1_Death;
			if (!sliding) return;

			// Hazard / wall reactions only while still alive-and-reeling (Hitstun). A flashing corpse
			// must not re-take hazard damage, re-score off spikes, or spam the wall hit sound.
			if (Current is St_En1_Hitstun)
			{
				// gameObject.layer is a layer INDEX; LayerMask.NameToLayer compares indices.
				// Pit crossings are handled in OnPhysics, so this is mainly Damage / walls.
				int layer = collision.gameObject.layer;

				if (layer == LayerMask.NameToLayer("Damage"))
					HurtByHazard();              // hit spikes mid-hitstun
				else if (layer == LayerMask.NameToLayer("Obstacle"))
				{
					// A wall flush against a pit would bounce the enemy straight back out before its
					// centre ever crossed the gap. If any part of the body is already over the pit when
					// it hits the wall, let the pit win — drop in instead of bouncing off.
					if (OverlapsPit())
						SetState<St_En1_Falling>();
					else
						HurtByHazard();          // slammed into a wall mid-hitstun — costs a dot (cooldown-gated)
				}
			}

			// Knocked into another enemy: pass the hit on whether we're reeling (Hitstun) or skidding
			// out as a corpse (Death). It takes damage + hitstun and can knock into the next — a chain.
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
			ScoreUI.Instance?.AddHazardDamage(1, transform.position); // 20 per point of damage dealt
			TakeDamage();
		}

		/// <summary>
		/// A knocked enemy slammed into us — take a hit and join the chain. Guarded so an enemy
		/// already reeling / dying isn't re-hit: two knocked enemies can't ping-pong damage forever,
		/// and each fresh enemy the chain reaches is hit exactly once. Shares the hazard cooldown.
		/// </summary>
		public void HitByEnemy()
		{
			if (Current is St_En1_Hitstun || Current is St_En1_Death || Current is St_En1_Falling) return;
			if (Time.time < damageReadyAt) return;
			damageReadyAt = Time.time + damageCooldown;

			PlayHit();
			// A knocked enemy slamming into this one scores like a player bounce, feeding the SAME
			// flying combo — so a chain reaction climbs 10 + 20 + 30 + 40 ... down the line.
			ScoreUI.Instance?.AddFlyingHit(transform.position);
			TakeDamage();
		}

		// A one-shot knockback direction set by an external bumper (the Beeg Dwarf's spin) so the victim
		// reels away from THAT source instead of from the player. Consumed once on entering hitstun.
		[HideInInspector] public bool		hasExternalKnockback;
		[HideInInspector] public Vector2	externalKnockbackDir;

		/// <summary>
		/// Knocked into hitstun by another enemy's attack — the Beeg Dwarf's spin — flying away from
		/// <paramref name="fromPosition"/>. Sends this enemy reeling and scores it for the player like any
		/// other chain hit. Self-guarded against re-hitting a reeling / dying enemy, and shares the hazard
		/// damage cooldown so one pass of the spin can't machine-gun the same victim.
		/// </summary>
		public void BumpIntoHitstun(Vector2 fromPosition)
		{
			if (Current is St_En1_Hitstun || Current is St_En1_Death || Current is St_En1_Falling) return;
			if (Time.time < damageReadyAt) return;
			damageReadyAt = Time.time + damageCooldown;

			externalKnockbackDir = (Vector2)transform.position - fromPosition;
			hasExternalKnockback = externalKnockbackDir.sqrMagnitude > 0.0001f;

			PlayHit();
			ScoreUI.Instance?.AddFlyingHit(transform.position); // points to the player for the spin's hit
			TakeDamage();                                       // spend a dot; reel into hitstun
		}

		/// <summary>
		/// Spends one health dot. Surviving a hit re-enters hitstun; reaching zero dies. The hazard
		/// coalescing cooldown is applied by the caller (HurtByHazard), not here, so player hits and
		/// the spikes they cause both land.
		/// </summary>
		public void TakeDamage(int amount = 1)
		{
			if (Current is St_En1_Death || Current is St_En1_Falling) return;

			health = Mathf.Max(0, health - amount);
			if (healthBar != null) healthBar.SetHealth(health);

			// Dead or alive, a hit knocks them into the slide. St_En1_Hitstun routes a dead enemy on
			// to St_En1_Death (flash + destroy) when the slide settles, so corpses skid before they go.
			SetState<St_En1_Hitstun>();
		}

		// Plays the shared hit clip (it lives on the player) through the SfxManager, which (a) survives
		// this enemy being destroyed, (b) isn't masked by the player's own source, and (c) attenuates
		// stacked copies — so a chain reaction that hits many enemies at once stays punchy instead of
		// summing into a roar.
		public void PlayHit()
		{
			AudioClip clip = playerController != null ? playerController.hit : null;
			if (clip == null) return;

			SfxManager.Play(clip, 0.6f);
		}

		/// <summary>
		/// States in which this enemy is the aggressor: a flying player that hits it gets hurt instead of
		/// damaging it (see St_Pl_Base). Only the fling lunge qualifies — the Beeg Dwarf's released spin
		/// instead REPELS a spinning player unharmed (St_Pl_Base.BounceOffEnemySpin), so the spin's
		/// wind-up, not the spin itself, is the window to punish the dwarf.
		/// </summary>
		public bool IsDealingContactDamage => Current is St_En1_Fling;

		/// <summary>True while the released spin is active (the attack or its wind-down) — it repels a spinning player.</summary>
		public bool IsSpinning => Current is St_En1_SpinAttack || Current is St_En1_SpinRecover;

		// Knocks every OTHER enemy currently overlapping the dwarf's (spin-sized) body into hitstun — the
		// spin sweep. Shared by St_En1_SpinAttack and St_En1_SpinRecover.
		public void BumpNearbyEnemies()
		{
			if (collider == null) return;

			Vector2 center = collider.bounds.center;
			Collider2D[] hits = Physics2D.OverlapCircleAll(center, collider.bounds.extents.x, 1 << gameObject.layer);

			foreach (Collider2D c in hits)
			{
				EnemyController other = c.GetComponentInParent<EnemyController>();
				if (other != null && other != this)
					other.BumpIntoHitstun(center);
			}
		}

		const float SpinBounceDecay = 12f;

		/// <summary>A player striking the dwarf mid-spin kicks it back a little — a recoil that decays away.</summary>
		public void AddSpinBounce(Vector2 fromPoint)
		{
			Vector2 away = (Vector2)transform.position - fromPoint;
			if (away.sqrMagnitude < 0.0001f) away = UnityEngine.Random.insideUnitCircle;

			spinBounceVelocity = away.normalized * spinHitBounceSpeed;
		}

		/// <summary>Applies and decays the mid-spin recoil; the spin states call this each frame.</summary>
		public void ApplySpinBounce()
		{
			if (spinBounceVelocity.sqrMagnitude < 0.0001f) return;

			MoveSafely(spinBounceVelocity * Time.deltaTime, avoidDamage: false);
			spinBounceVelocity = Vector2.Lerp(spinBounceVelocity, Vector2.zero, SpinBounceDecay * Time.deltaTime);
		}

		/// <summary>
		/// True when a spin-capable dwarf has detected the player within <see cref="spinDetectRange"/> and
		/// its cooldown is up — its cue to drop whatever it's doing (waiting, wandering, approaching) and
		/// charge a spin. The wide detect range means it engages from afar instead of bumping in close first.
		/// </summary>
		public bool WantsSpinAttack() =>
			canSpinAttack
			&& Time.time >= spinReadyAt
			&& playerController != null
			&& Vector2.Distance(transform.position, playerController.Position) <= spinDetectRange;

		/// <summary>Resets the spin cadence so the next SpinSoundTick fires promptly.</summary>
		public void ResetSpinSound() => _spinSoundTimer = 0f;

		/// <summary>
		/// Plays the deep spin whoosh on a repeating interval whose rate and pitch both rise with
		/// intensity (0..1) — the Beeg Dwarf's take on the player's spin sound, an octave down. Charging
		/// passes the wind-up progress; the attack holds it at full.
		/// </summary>
		public void SpinSoundTick(float t)
		{
			_spinSoundTimer -= Time.deltaTime;
			if (_spinSoundTimer > 0f) return;

			// Through the SfxManager so several spinning dwarves (and the player's own spin) don't sum
			// into a roar — the per-spin pitch still rides along.
			if (spinSound != null)
				SfxManager.Play(spinSound, spinVolume, Mathf.Lerp(spinPitchLow, spinPitchHigh, Mathf.Clamp01(t)));

			_spinSoundTimer = Mathf.Lerp(spinIntervalMax, spinIntervalMin, Mathf.Clamp01(t));
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

			// Dedicated source for the spin one-shots so their per-spin pitch doesn't bend the other
			// SFX played through audioSource — mirrors how the player runs its own spinSource.
			if (spinAudioSource == null)
			{
				spinAudioSource = gameObject.AddComponent<AudioSource>();
				spinAudioSource.playOnAwake = false;
			}

			// No bespoke clip wired? Borrow the player's spin whoosh — the dwarf plays it an octave down.
			if (spinSound == null && playerController != null)
				spinSound = playerController.spin;

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

			// Slide over Pit tiles instead of bouncing off their solid edge, so the body can carry over
			// the gap before it drops in. The pit queries (OverPit / OverlapsPit) ignore this exclusion.
			if (collider != null)
				collider.excludeLayers = collider.excludeLayers.value | LayerMask.GetMask("Pits");

			restScale = transform.localScale; // remember the rest size for the spin grow/shrink

			health    = maxHealth;
			healthBar = EnemyHealthBar.Create(transform, maxHealth, healthBarHeight);

			_prevCenter = collider != null ? (Vector2)collider.bounds.center : (Vector2)transform.position;

			// Join the live tallies — refunded the moment we die (Death/Falling) or, failing that, on destroy.
			_countedAlive = true;
			_countedCost  = spawnCost;
			AliveCount++;
			AliveCost += _countedCost;
		}

		// Backstop for the live tally: catches an enemy removed without going through a death state (e.g.
		// knocked off-screen by the Beeg cutscene, or a scene unload). StopCounting is idempotent, so an
		// enemy that already decremented on death isn't counted twice.
		private void OnDestroy() => StopCounting();

		protected override void OnPhysics()
		{
			// While the body is intangible — flung off-screen by the Beeg Dwarf's landing, which disables
			// the collider (CutsceneBounceOff) — it must not be claimed by a pit or hazard on the way out.
			// A disabled collider means we're not interacting with the world (also true while already
			// falling), so there's nothing for these reactions to do.
			if (collider == null || !collider.enabled) return;

			// Drop into a pit once the body's CENTRE passes over it — not once it's fully inside. A pit
			// thinner than the enemy can never wrap all four corners, so the old full-overlap test let a
			// knocked enemy slide straight across it. Centre-crossing (with a swept check below) claims
			// the enemy on any pit, however thin, the moment its middle is carried over the gap.
			// A spinning dwarf (attack + its wind-down) can't fall into pits — it carves over them.
			if (!(Current is St_En1_Falling || Current is St_En1_Death
				|| Current is St_En1_SpinAttack || Current is St_En1_SpinRecover) && CenterCrossedPit())
			{
				SetState<St_En1_Falling>();
				return;
			};

			// Only a reeling enemy (Hitstun) takes spike damage from being knocked in — that's the payoff.
			// A settling slide (Stopping), a lunge (Fling) or any walking state never gets hurt just for
			// touching a hazard, so an enemy that comes to rest on spikes can simply walk back off them.
			// A spinning dwarf is the exception: carving its big spin into spikes hurts it (it doesn't
			// build velocity, so this is an overlap test rather than the velocity-driven look-ahead).
			if (Current is St_En1_Hitstun && DamageWallAhead())
				HurtByHazard();
			else if ((Current is St_En1_SpinAttack || Current is St_En1_SpinRecover) && OverlapsDamage())
				HurtByHazard();

			// Remember where the centre is so next step's swept test spans the gap it travelled.
			if (collider != null) _prevCenter = collider.bounds.center;
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
				playerController.Position) <= detectionRadius;

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

		// True when any part of the body (approximated by its bounds circle) is over a pit. Used to let
		// a pit claim an enemy that hits a wall flush against it, instead of bouncing back out.
		private bool OverlapsPit() =>
			collider != null
			&& Physics2D.OverlapCircle(collider.bounds.center, collider.bounds.extents.x, LayerMask.GetMask("Pits")) != null;

		// True when the body overlaps a Damage (spike) tile — the spin attack uses this to take damage for
		// carving into spikes, since it moves by transform and so has no velocity to look ahead along.
		private bool OverlapsDamage() =>
			collider != null
			&& Physics2D.OverlapCircle(collider.bounds.center, collider.bounds.extents.x, LayerMask.GetMask("Damage")) != null;

		// True when the collider's centre is over a pit, OR swept across one since the last physics step.
		// The swept Linecast catches a fast knockback that would otherwise tunnel its centre clean over
		// a thin pit between FixedUpdates. Both queries ignore the collider's own Pit exclusion, so they
		// still see the pit the body is set to slide over.
		private bool CenterCrossedPit()
		{
			if (collider == null) return false;

			int     pits   = LayerMask.GetMask("Pits");
			Vector2 center = collider.bounds.center;

			return OverPit(center)
				|| Physics2D.Linecast(_prevCenter, center, pits).collider != null;
		}

		/// <summary>
		/// Settle into a transform-driven AI movement state (Wait / Wander / Approach / Pause): switch the
		/// body to Kinematic and clear any leftover velocity. These states walk by moving the transform and
		/// assume a velocity of ~0; left Dynamic, a collision — e.g. the player bouncing back after being
		/// hurt — shoves the body and leaves it drifting while its walk facing flip-flops. Mirrors what
		/// St_En1_SpinAttack already does for its scripted glide.
		/// </summary>
		public void EnterAIMovement()
		{
			if (rigidbody == null) return;

			rigidbody.bodyType       = RigidbodyType2D.Kinematic;
			rigidbody.linearVelocity = Vector2.zero;
		}

		// Moves the enemy by `displacement`, but never lets it advance into something it shouldn't cross:
		// Obstacle walls always, plus Pit and (unless avoidDamage is false — the spin carves spikes) Damage
		// tiles, and never off the camera view. A body already touching one of these can still slide along
		// it or back out (it just can't push deeper), so an enemy knocked into a wall isn't trapped. Returns
		// true if it actually moved.
		public bool MoveSafely(Vector2 displacement, bool avoidDamage = true)
		{
			if (collider == null || displacement == Vector2.zero)
			{
				transform.position += (Vector3)displacement;
				return true;
			};

			Vector2 center = collider.bounds.center;

			// Keep the body on-screen: clamp the destination to the camera view so it can't walk off the
			// edge, while still letting an enemy that spawned (or was knocked) off-screen path back in.
			displacement = ClampToView(center, center + displacement) - center;
			if (displacement == Vector2.zero)
				return false; // the whole step was a walk off-screen — blocked

			int mask = LayerMask.GetMask("Obstacle", "Pits");
			if (avoidDamage) mask |= LayerMask.GetMask("Damage");

			displacement = SlideOutOfSolids(center, displacement, collider.bounds.extents.x, mask);
			if (displacement == Vector2.zero)
				return false; // fully blocked this step

			transform.position += (Vector3)displacement;
			return true;
		}

		// Clamps `displacement` so the body never advances DEEPER into any solid on `mask`. For each solid
		// the step would touch, it measures the real separation (Collider2D.Distance) and removes only the
		// component of motion heading into that solid past its surface — leaving sliding-along and backing-
		// out motion intact. Unlike a boolean overlap test, this is penetration-based, so it holds for ANY
		// body size: the big dwarf, whose radius is perpetually inside some wall, is still stopped at the
		// surface instead of being waved through by an "already overlapping, move freely" shortcut.
		Vector2 SlideOutOfSolids(Vector2 center, Vector2 displacement, float radius, int mask)
		{
			foreach (Collider2D solid in Physics2D.OverlapCircleAll(center + displacement, radius, mask))
			{
				if (solid == null) continue;

				// Enemies sit on the Obstacle layer too (so the flying player bounces off them), so this
				// query also returns THIS enemy and its neighbours. Skip every enemy collider — only real
				// walls / pits / spikes should block movement (and Distance() throws on a collider vs itself).
				if (solid.GetComponentInParent<EnemyController>() != null) continue;

				ColliderDistance2D cd = collider.Distance(solid);

				// Direction pointing OUT of the solid (from its nearest surface point toward the body).
				Vector2 outward = (Vector2)collider.bounds.center - cd.pointB;
				if (outward.sqrMagnitude < 1e-8f) continue; // centred on the surface — nothing to resolve
				outward.Normalize();

				float into    = -Vector2.Dot(displacement, outward); // >0 means heading into the solid
				float allowed = Mathf.Max(cd.distance, 0f);          // gap to the surface (0 if overlapping)
				if (into > allowed)
					displacement += outward * (into - allowed);      // cancel the excess inward motion
			}
			return displacement;
		}

		Camera _camera;

		// Clamps `to` to the camera view (inset by the body's half-size), but never tighter than `from`
		// on an axis the body is already past — so an off-screen enemy can still path back in while none
		// on-screen can walk out. Returns `to` unchanged if there's no camera to bound against.
		Vector2 ClampToView(Vector2 from, Vector2 to)
		{
			if (_camera == null) _camera = Camera.main;
			if (_camera == null) return to;

			Vector2 min = _camera.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
			Vector2 max = _camera.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
			Vector2 ext = collider.bounds.extents;

			return new Vector2(
				ClampAxis(from.x, to.x, min.x + ext.x, max.x - ext.x),
				ClampAxis(from.y, to.y, min.y + ext.y, max.y - ext.y));
		}

		// Per-axis: hold inside [lo, hi], but if the body is already past a bound only block it from going
		// FURTHER out — moving back toward the valid range is always allowed.
		static float ClampAxis(float from, float to, float lo, float hi)
		{
			if (lo > hi) return to;                       // view smaller than the body — don't fight it
			if (to < lo) return Mathf.Min(Mathf.Max(to, from), lo);
			if (to > hi) return Mathf.Max(Mathf.Min(to, from), hi);
			return to;
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
