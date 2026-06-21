using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen black overlay used to fade between scenes / states.
///
/// Self-contained: on Awake it builds its own Canvas + stretched black Image + CanvasGroup on a
/// child object, so it works whether it is authored in a scene or spawned bare at runtime (see
/// <see cref="hoZer.St_Cs_ScreenFadeBase"/>, which instantiates one on demand). If instead it is
/// placed by hand on a GameObject that already carries a CanvasGroup under a Canvas, that existing
/// setup is reused untouched.
///
/// States drive it directly through <see cref="Alpha"/> (0 = clear, 1 = black). The
/// <see cref="FadeOut"/> / <see cref="FadeIn"/> coroutines remain for callers that animate it
/// themselves (e.g. GameManager's transition state).
/// </summary>
public class ScreenFader : MonoBehaviour
{
    [Header("Timing")]
    public float FadeOutDuration = 0.4f; // screen goes black
    public float FadeInDuration = 0.4f;  // screen clears
    public AnimationCurve Ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private CanvasGroup _group;

    /// <summary>Overlay opacity: 0 is fully clear, 1 is fully black. Also gates UI raycasts.</summary>
    public float Alpha
    {
        get { EnsureOverlay(); return _group.alpha; }
        set
        {
            EnsureOverlay();
            _group.alpha = Mathf.Clamp01(value);
            _group.blocksRaycasts = _group.alpha > 0.001f;
        }
    }

    private void Awake() => EnsureOverlay();

    /// <summary>
    /// Resolves the CanvasGroup we animate. Prefers an existing one (hand-authored setup);
    /// otherwise builds a self-contained full-screen black overlay on a child object.
    /// </summary>
    private void EnsureOverlay()
    {
        if (_group != null) return;

        // Hand-authored case: already a CanvasGroup under a Canvas — reuse it as-is.
        _group = GetComponent<CanvasGroup>();
        if (_group != null && GetComponentInParent<Canvas>() != null)
        {
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            return;
        }

        // Spawned-bare case: construct the overlay on a child object.
        var overlay = new GameObject("Overlay", typeof(RectTransform));
        overlay.transform.SetParent(transform, false);

        var canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // above gameplay UI
        overlay.AddComponent<GraphicRaycaster>();

        var image = overlay.AddComponent<Image>();
        image.color = Color.black;
        RectTransform rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _group = overlay.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
    }

    /// <summary>Fades to black. Yield on this before loading the next scene.</summary>
    public IEnumerator FadeOut() => Animate(0f, 1f, FadeOutDuration);

    /// <summary>Clears the black overlay. Yield on this after the new scene has loaded.</summary>
    public IEnumerator FadeIn() => Animate(1f, 0f, FadeInDuration);

    private IEnumerator Animate(float from, float to, float duration)
    {
        EnsureOverlay();
        float t = 0f;
        while (t < 1f)
        {
            t = duration > 0f ? Mathf.Min(t + Time.unscaledDeltaTime / duration, 1f) : 1f;
            Alpha = Mathf.LerpUnclamped(from, to, Ease.Evaluate(t));
            yield return null;
        }
        Alpha = to;
    }
}
