using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Attach to each Button object in any menu panel.
/// - Hover: scales up the image target
/// - Click: squash then release
/// - Entry: elastic bounce-in, triggered by MenuPanel.AnimateIn with a stagger delay
///
/// Set AnimationTarget to the child Image's RectTransform so the button hitbox stays fixed.
/// Wire sounds in the inspector; entry delay is set per-button to control stagger order.
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("Target")]
    [Tooltip("Image RectTransform to animate. Defaults to this object's RectTransform.")]
    public RectTransform AnimationTarget;

    [Header("Hover")]
    public float HoverScale    = 1.12f;
    public float HoverLift     = 8f;
    public float HoverDuration = 0.12f;

    [Header("Click")]
    public float ClickSquash   = 0.85f;
    public float ClickDuration = 0.07f;

    [Header("Entry Bounce")]
    public float EntryDuration = 0.45f;
    [Tooltip("Curve driving scale from 0→1 with overshoot. Leave empty for default elastic bounce.")]
    public AnimationCurve EntryCurve;

    [Header("Audio")]
    public AudioClip HoverSound;
    public AudioClip ClickSound;
    [Range(0f, 1f)] public float SoundVolume = 1f;

    [Header("Spin Letter (logo only)")]
    [Tooltip("Assign a child RectTransform (one letter sprite) to spin it while hovered.")]
    public RectTransform SpinLetter;
    public float SpinSpeed = 720f;

    private RectTransform _target;
    private Vector3       _baseScale;
    private Vector2       _basePosition;
    private Coroutine     _scaleRoutine;
    private Coroutine     _moveRoutine;
    private Coroutine     _spinRoutine;
    private bool          _pointerDown;

    private void Awake()
    {
        _target       = AnimationTarget != null ? AnimationTarget : GetComponent<RectTransform>();
        _baseScale    = _target.localScale;
        _basePosition = _target.anchoredPosition;

        if (EntryCurve == null || EntryCurve.keys.Length == 0)
            EntryCurve = BuildElasticCurve();
    }

    // Called by MenuPanel.AnimateIn with the stagger delay already applied.
    public IEnumerator BounceIn(float delay)
    {
        _target.localScale = Vector3.zero;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.deltaTime / EntryDuration, 1f);
            _target.localScale = _baseScale * EntryCurve.Evaluate(t);
            yield return null;
        }
        _target.localScale = _baseScale;
    }

    // Snap to resting state with no animation (used by MenuPanel.ShowImmediate).
    public void ResetScale()
    {
        _target.localScale       = _baseScale;
        _target.anchoredPosition = _basePosition;
    }

    // ── Pointer events ───────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData _)
    {
        PlaySound(HoverSound);
        ScaleTo(HoverScale, HoverDuration);
        MoveTo(_basePosition + Vector2.up * HoverLift, HoverDuration);
        StartLetterSpin();
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (!_pointerDown)
        {
            ScaleTo(1f, HoverDuration);
            MoveTo(_basePosition, HoverDuration);
        }
        StopLetterSpin();
    }

    public void OnPointerDown(PointerEventData _)
    {
        _pointerDown = true;
        PlaySound(ClickSound);
        ScaleTo(ClickSquash, ClickDuration);
        MoveTo(_basePosition, ClickDuration);
    }

    public void OnPointerUp(PointerEventData _)
    {
        _pointerDown = false;
        ScaleTo(1f, HoverDuration);
        MoveTo(_basePosition + Vector2.up * HoverLift, HoverDuration);
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void ScaleTo(float multiplier, float duration)
    {
        if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
        _scaleRoutine = StartCoroutine(ScaleRoutine(_baseScale * multiplier, duration));
    }

    private void MoveTo(Vector2 to, float duration)
    {
        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(MoveRoutine(to, duration));
    }

    private IEnumerator ScaleRoutine(Vector3 to, float duration)
    {
        Vector3 from = _target.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.deltaTime / duration, 1f);
            _target.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }
        _target.localScale = to;
    }

    private IEnumerator MoveRoutine(Vector2 to, float duration)
    {
        Vector2 from = _target.anchoredPosition;
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.deltaTime / duration, 1f);
            _target.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }
        _target.anchoredPosition = to;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(clip, pos, SoundVolume);
    }

    private void StartLetterSpin()
    {
        if (SpinLetter == null) return;
        if (_spinRoutine != null) StopCoroutine(_spinRoutine);
        _spinRoutine = StartCoroutine(SpinRoutine());
    }

    private void StopLetterSpin()
    {
        if (SpinLetter == null) return;
        if (_spinRoutine != null) { StopCoroutine(_spinRoutine); _spinRoutine = null; }
    }

    private IEnumerator SpinRoutine()
    {
        while (true)
        {
            SpinLetter.Rotate(0f, 0f, SpinSpeed * Time.deltaTime);
            yield return null;
        }
    }

private static AnimationCurve BuildElasticCurve()
    {
        var curve = new AnimationCurve(
            new Keyframe(0f,    0f,    0f,  6f),
            new Keyframe(0.55f, 1.25f, 2f,  -2f),
            new Keyframe(0.75f, 0.88f, 0f,  2f),
            new Keyframe(0.9f,  1.06f, 1f,  -1f),
            new Keyframe(1f,    1f,    0f,  0f)
        );
        return curve;
    }
}
