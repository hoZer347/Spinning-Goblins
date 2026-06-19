using UnityEngine;
using UnityEngine.InputSystem;
using AdequateGames;

public class PlayerStateMachine : StateMachine<PlayerStateMachine>
{
    [Header("References")]
    public Rigidbody2D Rigidbody;
    public Collider2D Collider;
    public LineRenderer DragLine;

    [Header("Launch")]
    public float LaunchForceMultiplier = 7f;
    public float MaxDragDistance = 2.5f;

    [Header("Movement")]
    [Range(0f, 10f)] public float LinearDamping = 3f;
    [Range(0f, 20f)] public float StickyDamping = 8f;
    [Range(0f, 10f)] public float StickyThreshold = 3f;

    [Header("Layers")]
    public LayerMask SpikeLayer;

    [Header("Death UI")]
    public GameObject DeathPanel;

    [HideInInspector] public Vector2 SpawnPosition;
    [HideInInspector] public Vector2 LaunchForce;
    [HideInInspector] public Vector2 DragClickPosition;

    protected override void OnStart()
    {
        SpawnPosition = transform.position;
        Rigidbody.gravityScale = 0f;
        Rigidbody.linearDamping = LinearDamping;
        if (DeathPanel != null) DeathPanel.SetActive(false);
        SetState<St_Pl_Idle>();
    }

    protected override void OnUpdate()
    {
        if (Rigidbody != null)
            Rigidbody.linearDamping = LinearDamping;

        if (Mouse.current.leftButton.wasPressedThisFrame
            && !(Current is St_Pl_Dragging)
            && !(Current is St_Pl_Dead))
        {
            DragClickPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            SetState<St_Pl_Dragging>();
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (Current is St_Pl_Flying && ((1 << col.gameObject.layer) & SpikeLayer.value) != 0)
            SetState<St_Pl_Dead>();
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (Current is St_Pl_Flying && ((1 << col.gameObject.layer) & SpikeLayer.value) != 0)
            SetState<St_Pl_Dead>();
    }
}
