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
    public float SpinRate      = 90f; // degrees per world-unit of speed
    public float SpinDecelRate = 8f;  // how fast spin winds down after flight ends

    [Header("Squash & Stretch")]
    public float FlightStretch   = 0.4f;
    public float SquashAmount    = 0.4f;
    public float SquashDuration  = 0.1f;
    public float PostStretchTime = 0.15f;

    [Header("Wall Lookahead")]
    public float WallLookaheadDist = 0.8f;

    [Header("Smoothing")]
    public float ScaleSmoothing = 16f;

    private PlayerController _player;
    private Vector3          _baseScale;
    private Vector3          _targetScale;
    private float            _spinAngle;
    private float            _spinVelocity;
    private float            _squashTimer;
    private float            _postStretchTimer;
    private Vector2          _squashNormal;
    private Vector2          _preImpactVelocity;
    private bool             _wasNearWall;
    private LayerMask        _walls;
    private ContactPoint2D[] _contacts = new ContactPoint2D[1];

    private void Awake() => _player = GetComponentInParent<PlayerController>();

    private void Start()
    {
        _baseScale   = transform.localScale;
        _targetScale = _baseScale;
        _walls       = _player.ObstacleLayer | _player.DamageLayer;
    }

    private void Update()
    {
        if (_player.Current is St_Pl_Dragging)
            UpdateDrag();
        else if (_player.Current is St_Pl_Flying)
            UpdateFlight();
        else
        {
            // Spin winds down in the same direction it was already going.
            _spinVelocity *= Mathf.Max(0f, 1f - SpinDecelRate * Time.deltaTime);
            _spinAngle    += _spinVelocity * Time.deltaTime;
            _targetScale   = _baseScale;
        }

        transform.localScale    = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * ScaleSmoothing);
        transform.localRotation = Quaternion.Euler(0f, 0f, _spinAngle);
    }

    private void UpdateDrag()
    {
        if (_player.LaunchForce.sqrMagnitude < 0.01f)
        {
            _targetScale = _baseScale;
            return;
        }

        float t       = Mathf.Clamp01(_player.LaunchForce.magnitude / (_player.MaxDragDistance * _player.LaunchForceMultiplier));
        float stretch = 1f + t * DragMaxStretch;

        float angle  = Mathf.Atan2(_player.LaunchForce.y, _player.LaunchForce.x) * Mathf.Rad2Deg - 90f;
        _spinAngle   = Mathf.LerpAngle(_spinAngle, angle, Time.deltaTime * ScaleSmoothing);
        _targetScale = new Vector3(_baseScale.x / stretch, _baseScale.y * stretch, _baseScale.z);
    }

    private void UpdateFlight()
    {
        float speed = _player.Rigidbody.linearVelocity.magnitude;

        Vector2 velocity = _player.Rigidbody.linearVelocity;

        // Wall contact: store pre-impact velocity and wall normal for stretch frame calculation.
        if (_player.Rigidbody.GetContacts(_contacts) > 0 && _squashTimer <= 0f)
        {
            _preImpactVelocity = velocity;
            _squashNormal      = _contacts[0].normal;
            _squashTimer       = SquashDuration;
            _postStretchTimer  = 0f;
            _spinVelocity      = 0f;
            // Align local Y with wall normal so squash compresses into the surface.
            _spinAngle = Mathf.Atan2(_squashNormal.y, _squashNormal.x) * Mathf.Rad2Deg - 90f;
        }

        if (_squashTimer > 0f)
        {
            // Hold wall-aligned angle during squash.
            _squashTimer -= Time.deltaTime;
            if (_squashTimer <= 0f) _postStretchTimer = PostStretchTime;

            float squash = 1f - SquashAmount;
            _targetScale = new Vector3(_baseScale.x / Mathf.Max(0.01f, squash), _baseScale.y * squash, _baseScale.z);
        }
        else if (_postStretchTimer > 0f)
        {
            // One-frame post-impact stretch in the reflected exit direction — spin continues.
            _postStretchTimer -= Time.deltaTime;
            Spin(speed);
            float stretch = 1f + FlightStretch;
            _targetScale  = new Vector3(_baseScale.x / stretch, _baseScale.y * stretch, _baseScale.z);
        }
        else
        {
            Spin(speed);

            bool nearWall = velocity.sqrMagnitude > 0f &&
                            Physics2D.Raycast(_player.transform.position, velocity.normalized, WallLookaheadDist, _walls);

            // Only trigger the stretch on the first frame the wall enters range.
            if (nearWall && !_wasNearWall)
            {
                float stretch = 1f + FlightStretch;
                _targetScale  = new Vector3(_baseScale.x / stretch, _baseScale.y * stretch, _baseScale.z);
            }
            else if (!nearWall)
            {
                _targetScale = _baseScale;
            }

            _wasNearWall = nearWall;
        }
    }

    private void Spin(float speed)
    {
        _spinVelocity  = speed * SpinRate;
        _spinAngle    += _spinVelocity * Time.deltaTime;
    }
}
