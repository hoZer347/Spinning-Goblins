using hoZer;
using UnityEngine;


/// <summary>
/// Frame-based sprite animation for an enemy — the counterpart to PlayerSpriteAnimator. Lives on the
/// enemy root (alongside its SpriteRenderer / EnemyController) and picks an animation from the
/// current state:
///   • Sleeping — the dormant St_En1_Sleeping pose.
///   • Idle     — waiting (St_En1_Wait / St_En1_Pause).
///   • Walk     — wandering / approaching; the directional set is chosen from the actual movement.
///   • Spin     — when hit by the player (hitstun / braking / death).
///   • Falling  — dropping into a pit.
/// It also squashes / stretches on a wall or hazard impact. Because the renderer shares the root
/// transform with the collider, the squash is skipped while Falling / FlingWindup own the scale.
/// </summary>
public class EnemySpriteAnimator : MonoBehaviour
{
    [Header("Frames (slice the dwarf sheets, then drop the frames here in order)")]
    public Sprite[] IdleFrames;
    public Sprite[] SleepingFrames;
    public Sprite[] WalkUpFrames;
    public Sprite[] WalkDownFrames;
    public Sprite[] WalkLeftFrames;
    public Sprite[] WalkRightFrames;
    public Sprite[] SpinFrames;     // hit by the player
    public Sprite[] FallingFrames;  // dropping into a pit

    [Header("Timing")]
    public float FramesPerSecond = 6f;

    [Header("Impact Squash & Stretch")]
    [Range(0f, 0.95f)] public float SquashAmount = 0.35f;
    public float SquashDuration = 0.08f;
    [Range(0f, 0.95f)] public float StretchAmount = 0.2f;
    public float StretchDuration = 0.14f;
    [Tooltip("Impact speed at which the crush reaches full strength.")]
    public float ImpactReferenceSpeed = 12f;
    [Tooltip("Crush/stretch multiplier at a full-speed impact; gentle bumps stay subtle.")]
    [Range(1f, 3f)] public float ImpactSpeedBoost = 1.4f;
    public float ScaleSmoothing = 16f;
    [Tooltip("Minimum movement per frame before the walk facing is updated.")]
    public float MoveEpsilon = 0.0015f;

    [Tooltip("Degrees past the 45° boundary the heading must turn before the walk direction flips. " +
             "Overlapping the switch thresholds this way stops the flicker on near-diagonal approaches.")]
    [Range(0f, 30f)] public float DirectionHysteresis = 12f;

    private SpriteRenderer  _renderer;
    private EnemyController _enemy;
    private Sprite[]        _current;
    private int             _frame;
    private float           _frameTimer;

    private Vector2         _lastPos;
    private enum Dir { Up, Down, Left, Right }
    private Dir             _dir = Dir.Down;

    private Vector3         _baseScale;
    private float           _squashTimer;
    private float           _stretchTimer;
    private bool            _squashAxisX;
    private bool            _wasInContact;
    private float           _impactFactor = 1f;
    private readonly ContactPoint2D[] _contacts = new ContactPoint2D[4];

    private void Awake()
    {
        _renderer  = GetComponent<SpriteRenderer>();
        _enemy     = GetComponent<EnemyController>();
        _baseScale = transform.localScale;
        _lastPos   = transform.position;
    }

    private void Update()
    {
        if (_enemy == null || _renderer == null) return;

        State state = _enemy.Current;

        TrackFacing();
        AdvanceFrames(state);
        UpdateImpactScale(state);
    }

    // The enemy walks by moving its transform, so read the real position delta (velocity is ~0
    // while wandering / approaching) and remember the last meaningful heading.
    private void TrackFacing()
    {
        Vector2 pos   = transform.position;
        Vector2 delta = pos - _lastPos;
        _lastPos = pos;

        if (delta.sqrMagnitude < MoveEpsilon * MoveEpsilon) return;

        // Keep the current facing until the heading turns more than (45° + hysteresis) away from it,
        // then snap to the new nearest cardinal. The wider exit threshold means a near-diagonal
        // heading (e.g. up-right) won't flip between Up and Right frame to frame.
        Vector2 f         = delta.normalized;
        float   switchCos = Mathf.Cos((45f + DirectionHysteresis) * Mathf.Deg2Rad);
        if (Vector2.Dot(f, DirVec(_dir)) < switchCos)
            _dir = NearestDir(f);
    }

    private void AdvanceFrames(State state)
    {
        Sprite[] frames = FramesFor(state);

        if (frames != _current)
        {
            _current    = frames;
            _frame      = 0;
            _frameTimer = 0f;
        }

        if (_current == null || _current.Length == 0) return;

        _frameTimer += FramesPerSecond * Time.deltaTime;
        while (_frameTimer >= 1f)
        {
            _frameTimer -= 1f;
            _frame = (_frame + 1) % _current.Length;
        }

        _renderer.sprite = _current[_frame];
    }

    private Sprite[] FramesFor(State state)
    {
        if (state is St_En1_Sleeping) return Use(SleepingFrames);
        if (state is St_En1_Falling)  return Use(FallingFrames, SpinFrames);
        if (state is St_En1_Hitstun || state is St_En1_Death) return Use(SpinFrames); // spin through the hit + death
        if (state is St_En1_Wander || state is St_En1_Approach
            || state is St_En1_Fling || state is St_En1_Stopping)
            return WalkFrames();
        return Use(IdleFrames); // Wait / Pause / FlingWindup / Die / anything else
    }

    // Pick the directional walk set for the committed facing; fall back to idle if unassigned.
    private Sprite[] WalkFrames()
    {
        Sprite[] set = _dir switch
        {
            Dir.Up   => WalkUpFrames,
            Dir.Down => WalkDownFrames,
            Dir.Left => WalkLeftFrames,
            _        => WalkRightFrames,
        };
        return Use(set, IdleFrames);
    }

    private static Vector2 DirVec(Dir d) => d switch
    {
        Dir.Up   => Vector2.up,
        Dir.Down => Vector2.down,
        Dir.Left => Vector2.left,
        _        => Vector2.right,
    };

    private static Dir NearestDir(Vector2 f) =>
        Mathf.Abs(f.x) >= Mathf.Abs(f.y)
            ? (f.x >= 0f ? Dir.Right : Dir.Left)
            : (f.y >= 0f ? Dir.Up    : Dir.Down);

    private Sprite[] Use(Sprite[] primary, Sprite[] fallback = null) =>
        primary != null && primary.Length > 0 ? primary : (fallback ?? IdleFrames);

    // Crush on a fresh wall / hazard contact while moving (the velocity-driven hit states), then
    // spring back through a stretch. Skipped while Falling / FlingWindup own the transform scale.
    private void UpdateImpactScale(State state)
    {
        if (state is St_En1_Falling || state is St_En1_FlingWindup) return;

        bool moving    = state is St_En1_Hitstun || state is St_En1_Stopping || state is St_En1_Fling;
        bool inContact = _enemy.rigidbody != null && _enemy.rigidbody.GetContacts(_contacts) > 0;

        if (inContact && !_wasInContact && moving && _squashTimer <= 0f && _stretchTimer <= 0f)
        {
            Vector2 normal = _contacts[0].normal;
            _squashAxisX   = Mathf.Abs(normal.x) > Mathf.Abs(normal.y);
            _squashTimer   = SquashDuration;

            float speedT  = ImpactReferenceSpeed > 0f
                ? Mathf.Clamp01(_enemy.rigidbody.linearVelocity.magnitude / ImpactReferenceSpeed)
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
            target = ScaleAlong(1f - s, 1f + s);
        }
        else if (_stretchTimer > 0f)
        {
            _stretchTimer -= Time.deltaTime;
            float s = Mathf.Clamp(StretchAmount * _impactFactor, 0f, 0.95f);
            target = ScaleAlong(1f + s, 1f - s);
        }
        else
        {
            target = _baseScale;
        }

        transform.localScale = Vector3.Lerp(transform.localScale, target, ScaleSmoothing * Time.deltaTime);
    }

    private Vector3 ScaleAlong(float along, float across) =>
        _squashAxisX
            ? new Vector3(_baseScale.x * along,  _baseScale.y * across, _baseScale.z)
            : new Vector3(_baseScale.x * across, _baseScale.y * along,  _baseScale.z);
}
