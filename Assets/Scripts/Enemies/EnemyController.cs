using UnityEngine;


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
		[SerializeField] public PlayerController	playerController;

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

		[Header("Approach Settings")]
		[HideInInspector] bool						playerSpotted		= false;
		[SerializeField] public float				approachSpeed		= 40f;

		[Header("Death Settings")]
		[SerializeField] public float				fallingDuration		= 1.0f;

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

			if (rigidbody != null)
			{
				// Top-down game: no gravity. And a Kinematic body only fires OnCollisionEnter2D
				// against the static walls/hazards when full kinematic contacts are enabled —
				// without this, kinematic-vs-static collisions are silent.
				rigidbody.gravityScale = 0f;
				rigidbody.useFullKinematicContacts = true;
			};
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
