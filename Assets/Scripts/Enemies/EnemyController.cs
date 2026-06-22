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

			if (enemyController.Length == 0)
				SceneManager.LoadScene(FindAnyObjectByType<CutsceneManager>()
					.NextScene);
		}

		private void OnCollisionEnter2D(Collision2D collision)
		{
			// gameObject.layer is a layer INDEX (e.g. 8); LayerMask.GetMask returns a BITMASK
			// (e.g. 256). Comparing them is never true — use NameToLayer to compare indices.
			int layer = collision.gameObject.layer;

			if (Current is St_En1_Hitstun)
			{
				if (layer == LayerMask.NameToLayer("Damage"))
					SetState<St_En1_Die>();
				else if (layer == LayerMask.NameToLayer("Pits"))
					SetState<St_En1_Falling>();
			};
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
		}

		protected override void OnPhysics()
		{
			// Pre-empt the wall bounce. Only while being knocked back (Hitstun / Stopping is the
			// Dynamic slide) — Wander/Approach move via transform, so they carry no velocity here.
			if (!(Current is St_En1_Hitstun || Current is St_En1_Stopping))
				return;

			// Look ahead along the slide direction and react to a hazard BEFORE the collision
			// resolves, so the body never actually bounces off it.
			int layer = PredictHazardLayer();

			if (layer == LayerMask.NameToLayer("Damage"))
				SetState<St_En1_Die>();
			else if (layer == LayerMask.NameToLayer("Pits"))
				SetState<St_En1_Falling>();
		}

		// The layer of a Damage/Pits collider the body is about to slide into this step, or -1.
		private int PredictHazardLayer()
		{
			if (rigidbody == null || collider == null)
				return -1;

			Vector2 velocity = rigidbody.linearVelocity;
			float   speed    = velocity.magnitude;

			if (speed < 0.01f)
				return -1; // not sliding — nothing to pre-empt

			float distance = speed * Time.fixedDeltaTime + WallCastSkin;

			RaycastHit2D hit = Physics2D.CircleCast(
				collider.bounds.center,
				collider.bounds.extents.x,
				velocity / speed,
				distance,
				LayerMask.GetMask("Damage", "Pits"));

			return hit.collider != null ? hit.collider.gameObject.layer : -1;
		}

		protected virtual bool PlayerInRange() =>
			Vector2.Distance(
				transform.position,
				playerController.transform.position) <= detectionRadius;

		public bool IsCenterOverPit()
		{
			if (collider == null) return false;
			// Only the player's center point counts, so brushing a pit edge won't drop you in.
			return Physics2D.OverlapPoint(collider.bounds.center, LayerMask.GetMask("Pits")) != null;
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
