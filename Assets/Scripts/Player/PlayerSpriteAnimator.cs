using UnityEngine;


/// <summary>
/// Attach to the SpriteRenderer child of the player (NOT the root) so the
/// physics collider is unaffected. Reads PlayerController state directly —
/// no velocity thresholds drive logic, only state-machine checks and physics queries.
/// </summary>
public class PlayerSpriteAnimator : MonoBehaviour
{
    [Header("Drag Stretch")]
    public float DragMaxStretch = 0.5f;

    [Header("Flight Spin")]
    public float SpinRate = 90f; // degrees per world-unit of speed

    [Header("Squash & Stretch")]
    public float FlightStretch = 0.4f;
    public float SquashAmount = 0.4f;
    public float SquashDuration = 0.1f;
    public float PostStretchTime = 0.15f;

    [Header("Wall Lookahead")]
    public float WallLookaheadDist = 0.8f;

    [Header("Smoothing")]
    public float ScaleSmoothing = 16f;

    private PlayerController _player;
    private Vector3 _baseScale;
    private Vector3 _targetScale;
    private float _spinAngle;
    private float _squashTimer;
    private float _postStretchTimer;
    private LayerMask _walls;
    private ContactPoint2D[] _contacts = new ContactPoint2D[1];

    private void Awake() => _player = GetComponentInParent<PlayerController>();

    private void Start()
    {
        _baseScale = transform.localScale;
        _targetScale = _baseScale;
        _walls = _player.ObstacleLayer | _player.DamageLayer;
    }

    private void Update()
    {
        if (_player.Current is St_Pl_Dragging)
            UpdateDrag();
        else if (_player.Current is St_Pl_Flying)
            UpdateFlight();
        else
        {
            _targetScale = _baseScale;
            _spinAngle = Mathf.LerpAngle(_spinAngle, 0f, Time.deltaTime * ScaleSmoothing * 0.5f);
        }

        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * ScaleSmoothing);
        transform.localRotation = Quaternion.Euler(0f, 0f, _spinAngle);
    }

    private void UpdateDrag()
    {
        if (_player.LaunchForce.sqrMagnitude < 0.01f)
        {
            _targetScale = _baseScale;
            return;
        }

        // Stretch proportional to how far back the player is pulled.
        float t = Mathf.Clamp01(_player.LaunchForce.magnitude / (_player.MaxDragDistance * _player.LaunchForceMultiplier));
        float stretch = 1f + t * DragMaxStretch;

        // Rotate so the long axis points in the launch direction.
        float angle = Mathf.Atan2(_player.LaunchForce.y, _player.LaunchForce.x) * Mathf.Rad2Deg - 90f;
        _spinAngle = Mathf.LerpAngle(_spinAngle, angle, Time.deltaTime * ScaleSmoothing);
        _targetScale = new Vector3(_baseScale.x / stretch, _baseScale.y * stretch, _baseScale.z);
    }

    private void UpdateFlight()
    {
        float speed = _player.Rigidbody.linearVelocity.magnitude;

        // Spin speed scales directly with velocity magnitude.
        _spinAngle += speed * SpinRate * Time.deltaTime;

        // Wall contact detected via live physics contacts — no velocity heuristics.
        if (_player.Rigidbody.GetContacts(_contacts) > 0 && _squashTimer <= 0f)
        {
            _squashTimer = SquashDuration;
            _postStretchTimer = 0f;
        }

        if (_squashTimer > 0f)
        {
            // Squash on contact.
            _squashTimer -= Time.deltaTime;
            if (_squashTimer <= 0f) _postStretchTimer = PostStretchTime;

            float squash = 1f - SquashAmount;
            _targetScale = new Vector3(_baseScale.x / Mathf.Max(0.01f, squash), _baseScale.y * squash, _baseScale.z);
        }
        else if (_postStretchTimer > 0f)
        {
            // Stretch follow-through right after the bounce.
            _postStretchTimer -= Time.deltaTime;
            float stretch = 1f + FlightStretch;
            _targetScale = new Vector3(_baseScale.x / stretch, _baseScale.y * stretch, _baseScale.z);
        }
        else
        {
            // Anticipation stretch: raycast ahead to detect an upcoming wall.
            Vector2 vel = _player.Rigidbody.linearVelocity;
            bool nearWall = vel.sqrMagnitude > 0f &&
                               Physics2D.Raycast(_player.transform.position, vel.normalized, WallLookaheadDist, _walls);

            float stretch = 1f + FlightStretch;
            _targetScale = nearWall
                ? new Vector3(_baseScale.x / stretch, _baseScale.y * stretch, _baseScale.z)
                : _baseScale;
        }
    }
}
