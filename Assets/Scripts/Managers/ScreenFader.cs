using System.Collections;
using UnityEngine;

/// <summary>
/// Full-screen black overlay that fades between scene loads.
/// Uses a CanvasGroup on this GameObject — no Animator required.
///
/// Setup: place on a full-screen Image child of the GameManager prefab Canvas
/// (Sort Order 100+). The CanvasGroup starts at alpha 0.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    [Header("Timing")]
    public float FadeOutDuration = 0.4f; // screen goes black
    public float FadeInDuration = 0.4f; // screen clears
    public AnimationCurve Ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private CanvasGroup _group;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
    }

    /// <summary>Fades to black. Await before loading the next scene.</summary>
    public IEnumerator FadeOut()
    {
        _group.blocksRaycasts = true;
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.unscaledDeltaTime / FadeOutDuration, 1f);
            _group.alpha = Ease.Evaluate(t);
            yield return null;
        }
        _group.alpha = 1f;
    }

    /// <summary>Clears the black overlay. Await after the new scene has loaded.</summary>
    public IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.unscaledDeltaTime / FadeInDuration, 1f);
            _group.alpha = 1f - Ease.Evaluate(t);
            yield return null;
        }
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
    }
}
