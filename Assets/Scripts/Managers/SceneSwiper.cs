using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(RectTransform))]
public class SceneSwiper : MonoBehaviour
{
    [Header("Timing")]
    public float SwipeInDuration  = 0.3f;
    public float SwipeOutDuration = 0.3f;
    public AnimationCurve Ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Audio")]
    public AudioSource AudioSource;
    public AudioClip   SwipeInSound;
    public AudioClip   SwipeOutSound;

    private RectTransform _rect;
    private bool _swipedIn;
    private bool _animating;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
        }
        else
        {
            Debug.LogWarning("[SceneSwiper] Awake — no Canvas found in parent hierarchy!", this);
        }

        // If not already under a DontDestroyOnLoad root (e.g. GameManager), make root persistent.
        if (transform.root.GetComponent<GameManager>() == null)
        {
            DontDestroyOnLoad(transform.root.gameObject);
        }

        Park();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Unity calls this whenever the rect's dimensions change — including a window / canvas resize.
    // While idle (parked off-screen) the old offset may no longer clear the new bounds and leave a
    // sliver of the cover showing, so re-park it. Skipped mid-swipe or while it's covering the screen.
    private void OnRectTransformDimensionsChange()
    {
        if (_rect != null && !_swipedIn && !_animating)
            Park();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_swipedIn)
            StartCoroutine(SwipeOut());
    }

    private float Width() => _rect.rect.width > 0f ? _rect.rect.width : Screen.width;

    private void Park()
    {
        float w = Width();
        _rect.anchoredPosition = new Vector2(w, 0f);
    }

    public IEnumerator SwipeIn()
    {
        _swipedIn = true;
        _animating = true;
        float w = Width();
        _rect.anchoredPosition = new Vector2(w, 0f);
        Play(SwipeInSound);
        yield return Slide(w, 0f, SwipeInDuration);
        _animating = false;
    }

    public IEnumerator SwipeOut()
    {
        _swipedIn = false;
        _animating = true;
        // Skip one frame so Time.unscaledDeltaTime doesn't include the scene-load spike,
        // which would cause Slide to complete instantly on the first iteration.
        yield return null;
        float w = Width();
        _rect.anchoredPosition = new Vector2(0f, 0f);
        Play(SwipeOutSound);
        yield return Slide(0f, -w, SwipeOutDuration);
        Park();
        _animating = false;
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && AudioSource != null)
            AudioSource.PlayOneShot(clip);
    }

    private IEnumerator Slide(float from, float to, float duration)
    {
        float start = Time.realtimeSinceStartup;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed = Time.realtimeSinceStartup - start;
            float t = Mathf.Clamp01(duration > 0f ? elapsed / duration : 1f);
            _rect.anchoredPosition = new Vector2(Mathf.Lerp(from, to, Ease.Evaluate(t)), 0f);
            yield return null;
        }
        _rect.anchoredPosition = new Vector2(to, 0f);
    }
}
