using UnityEngine;
using UnityEngine.InputSystem;
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

    protected override void OnUpdate()
    {
        if (Rigidbody != null)
            Rigidbody.linearDamping = LinearDamping;

        // Pits only swallow the player while resting (Idle), and only once the player's
        // center is actually inside a pit cell (not merely overlapping its edge).
        if (Current is St_Pl_Idle && IsCenterOverPit())
        {
            SetState<St_Pl_Falling>();
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && CanStartDrag())
        {
            // Starting a drag while your center sits on a pit drops you in instead of
            // launching. Checked only at the press (your origin), so dragging back over a
            // different pit still won't fall you.
            if (IsCenterOverPit())
            {
                SetState<St_Pl_Falling>();
                return;
            }

            DragClickPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            SetState<St_Pl_Dragging>();
        }
    }

    private bool CanStartDrag() =>
        !(Current is St_Pl_Dragging)
        && !(Current is St_Pl_Dead)
        && !(Current is St_Pl_IFrames)
        && !(Current is St_Pl_Falling);

    private bool IsCenterOverPit()
    {
        if (Collider == null) return false;
        // Only the player's center point counts, so brushing a pit edge won't drop you in.
        // Pits should be trigger colliders so the player can float over them.
        return Physics2D.OverlapPoint(Collider.bounds.center, PitsLayer) != null;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
		// Obstacle tiles bounce automatically via the physics material; nothing to do.
		if (InMask(col.gameObject.layer, DamageLayer))
            TakeDamage();
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        // Supports a Damage layer configured as a trigger as well.
        if (InMask(col.gameObject.layer, DamageLayer))
            TakeDamage();
    }

    /// <summary>
    /// Applies a hit: screen shake, respawn at the start, then a brief invulnerable window.
    /// For now any hit just respawns the player (no health pool yet).
    /// </summary>
    public void TakeDamage()
    {
        if (IsInvulnerable) return;

		
		CameraController cameraController = FindAnyObjectByType<CameraController>();
		if (cameraController != null)
			cameraController.SetState<St_Cm_Shake>();

        //CameraShake.Shake(ScreenShakeDuration, ScreenShakeMagnitude);

        // TODO: subtract from a health pool here, only respawn / go to St_Pl_Dead when depleted.
        RespawnAtStart();
        SetState<St_Pl_IFrames>();
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

        // Push the move into the physics world immediately so the next IsCenterOverPit()
        // query reads the new position. Without this the collider bounds stay at the old
        // (pit) location until the next FixedUpdate, which re-triggers the fall on respawn.
        Physics2D.SyncTransforms();
    }

    private static bool InMask(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;
}
