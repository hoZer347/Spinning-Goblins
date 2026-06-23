using hoZer;
using UnityEngine;


/// <summary>
/// Frame-based sprite animation for the player. Lives on the Sprite child (NOT the root, so the
/// collider is unaffected) and picks an animation from the player's current state:
///   • Idle     — steady <see cref="FramesPerSecond"/>.
///   • Spin     — while dragging / flying / braking; the frame rate scales with the same spin
///                intensity that drives the spin sound's pitch and rate (charge while dragging,
///                speed while flying), so the wheel visibly speeds up and winds down with it.
///   • Hitstun  — while in the post-hit i-frame window; reverts once the i-frames end.
/// While dragging it leans and stretches like a loaded slingshot — stretching along whichever axis
/// the pull favours and tilting only off the nearest axis so the goblin never leans past 45°. The
/// lean snaps back to upright the moment it launches. On a wall / enemy impact in flight it squashes
/// and stretches off the contact normal. The sprite is never spun — it stays upright.
/// </summary>
public class PlayerSpriteAnimator : MonoBehaviour
{
    [Header("Frames (slice the goblin sheets, then drop the frames here in order)")]
    public Sprite[] IdleFrames;
    public Sprite[] SpinFrames;
    public Sprite[] HitstunFrames;
    [Tooltip("Braking 'landing' animation — shown when a press during flight brakes into the stop.")]
    public Sprite[] LandingFrames;

    [Header("Timing")]
    [Tooltip("Playback rate for the steady animations (idle, hitstun).")]
    public float FramesPerSecond = 4f;

    [Tooltip("Spin frame rate at zero and full spin intensity (the same intensity that drives the spin sound).")]
    public float SpinFpsMin = 2f;
    public float SpinFpsMax = 24f;

    [Header("Drag Stretch")]
    [Tooltip("How far the sprite elongates along the pull at full draw (perpendicular thins to keep its volume).")]
    [Range(0f, 1.5f)] public float DragStretch = 0.7f;

    [Header("Impact Squash & Stretch")]
    [Tooltip("How far the sprite crushes along the impact axis on a wall / enemy hit (0 = none).")]
    [Range(0f, 0.95f)] public float SquashAmount = 0.6f;
    public float SquashDuration = 0.09f;
    [Tooltip("Stretch overshoot along the same axis as the sprite springs back.")]
    [Range(0f, 0.95f)] public float StretchAmount = 0.45f;
    public float StretchDuration = 0.16f;
    [Tooltip("Crush/stretch multiplier at a full-speed impact — walls hit hardest; gentle bumps stay subtle.")]
    [Range(1f, 3f)] public float ImpactSpeedBoost = 1.8f;
    [Tooltip("How quickly the scale / lean eases toward its target.")]
    public float ScaleSmoothing = 18f;

    private SpriteRenderer   _renderer;
    private PlayerController _player;
    private Sprite[]         _current;
    private int              _frame;
    private float            _frameTimer;

    private Vector3          _baseScale;
    private float            _squashTimer;
    private float            _stretchTimer;
    private bool             _squashAxisX;   // true = crush along X (side hit), false = along Y (floor / ceiling)
    private bool             _wasInContact;
    private float            _impactFactor = 1f; // speed-based crush multiplier captured at the last hit
    private readonly ContactPoint2D[] _contacts = new ContactPoint2D[4];

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _player   = GetComponentInParent<PlayerController>();

        transform.localRotation = Quaternion.identity;
        _baseScale = transform.localScale;
    }

    private void Update()
    {
        if (_player == null || _renderer == null) return;

        State state = _player.Current;

        AdvanceFrames(state);
        UpdatePose(state);
    }

    private void AdvanceFrames(State state)
    {
        Sprite[] frames = FramesFor(state);

        // Restart from the first frame whenever the active animation changes.
        if (frames != _current)
        {
            _current    = frames;
            _frame      = 0;
            _frameTimer = 0f;
        }

        if (_current == null || _current.Length == 0) return;

        _frameTimer += FpsFor(state) * Time.deltaTime;
        while (_frameTimer >= 1f)
        {
            _frameTimer -= 1f;
            _frame = (_frame + 1) % _current.Length;
        }

        _renderer.sprite = _current[_frame];
    }

    private void UpdatePose(State state)
    {
        if (state is St_Pl_Dragging)
        {
            ApplyDragPose();
            return;
        }

        // Snap upright leaving the drag (the launch); flight and everything else stays level.
        transform.localRotation = Quaternion.identity;
        UpdateImpactScale(state);
    }

    // Rotates the sprite so its top (+Y) always faces the launch direction, and stretches
    // along that axis like a loaded slingshot.
    private void ApplyDragPose()
    {
        _squashTimer = _stretchTimer = 0f;

        float   follow = ScaleSmoothing * Time.deltaTime;
        Vector2 dir    = _player.LaunchForce;

        if (dir.sqrMagnitude < 0.0001f)
        {
            transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.identity, follow);
            transform.localScale    = Vector3.Lerp(transform.localScale, _baseScale, follow);
            return;
        }

        dir.Normalize();
        float t      = _player.MaxLaunchPower > 0f
            ? Mathf.Clamp01(_player.LaunchForce.magnitude / _player.MaxLaunchPower)
            : 0f;

        float along  = 1f + t * DragStretch;
        float across = 1f / along;

        // Rotate so the sprite's +Y points toward the launch direction (full 360°).
        float tilt = -Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        // Stretch always along local Y (which now points toward the launch direction).
        Vector3 target = new Vector3(_baseScale.x * across, _baseScale.y * along, _baseScale.z);

        transform.localScale    = Vector3.Lerp(transform.localScale, target, follow);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(0f, 0f, tilt), follow);
    }

    // Crush on a fresh wall / enemy contact while moving, then spring back through a stretch.
    private void UpdateImpactScale(State state)
    {
        bool moving    = state is St_Pl_Flying || state is St_Pl_Stopping;
        bool inContact = _player.Rigidbody.GetContacts(_contacts) > 0;

        if (inContact && !_wasInContact && moving && _squashTimer <= 0f && _stretchTimer <= 0f)
        {
            Vector2 normal = _contacts[0].normal;
            _squashAxisX   = Mathf.Abs(normal.x) > Mathf.Abs(normal.y);
            _squashTimer   = SquashDuration;

            // Harder hits (walls, taken at full flight speed) crush far more than gentle bumps.
            float speedT  = _player.MaxLaunchPower > 0f
                ? Mathf.Clamp01(_player.Rigidbody.linearVelocity.magnitude / _player.MaxLaunchPower)
                : 0f;
            _impactFactor = Mathf.Lerp(0.5f, ImpactSpeedBoost, speedT);
        }
        _wasInContact = inContact;

        Vector3 target;
        if (_squashTimer > 0f)
        {
            _squashTimer -= Time.deltaTime;
            if (_squashTimer <= 0f) _stretchTimer = StretchDuration;
            float s = Mathf.Clamp(SquashAmount * _impactFactor, 0f, 0.95f);
            target = ScaleAlong(1f - s, 1f + s); // crush along axis, bulge across
        }
        else if (_stretchTimer > 0f)
        {
            _stretchTimer -= Time.deltaTime;
            float s = Mathf.Clamp(StretchAmount * _impactFactor, 0f, 0.95f);
            target = ScaleAlong(1f + s, 1f - s); // stretch along axis on rebound
        }
        else
        {
            target = _baseScale;
        }

        transform.localScale = Vector3.Lerp(transform.localScale, target, ScaleSmoothing * Time.deltaTime);
    }

    // Scale relative to the rest pose: `along` multiplies the impact axis, `across` the other.
    private Vector3 ScaleAlong(float along, float across) =>
        _squashAxisX
            ? new Vector3(_baseScale.x * along,  _baseScale.y * across, _baseScale.z)
            : new Vector3(_baseScale.x * across, _baseScale.y * along,  _baseScale.z);

    private Sprite[] FramesFor(State state)
    {
        if (state is St_Pl_Stopping)
            return LandingFrames != null && LandingFrames.Length > 0 ? LandingFrames : SpinFrames;
        if (IsSpinning(state))                                return SpinFrames;
        if (state is St_Pl_Hitstun || state is St_Pl_IFrames) return HitstunFrames;
        return IdleFrames;
    }

    private float FpsFor(State state)
    {
        // Spin advances at a rate tied to the spin intensity, so it matches the spin sound exactly.
        if (IsSpinning(state))
            return Mathf.Lerp(SpinFpsMin, SpinFpsMax, _player.CurrentSpinIntensity());

        return FramesPerSecond;
    }

    private static bool IsSpinning(State state) =>
        state is St_Pl_Dragging || state is St_Pl_Flying;
}
