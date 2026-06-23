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

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            Debug.Log($"[SceneSwiper] Awake — canvas set to ScreenSpaceOverlay sortOrder=999. root='{transform.root.name}'");
        }
        else
        {
            Debug.LogWarning("[SceneSwiper] Awake — no Canvas found in parent hierarchy!", this);
        }

        // If not already under a DontDestroyOnLoad root (e.g. GameManager), make root persistent.
        if (transform.root.GetComponent<GameManager>() == null)
        {
            Debug.Log($"[SceneSwiper] Awake — root '{transform.root.name}' has no GameManager, calling DontDestroyOnLoad.");
            DontDestroyOnLoad(transform.root.gameObject);
        }

        Park();
        Debug.Log($"[SceneSwiper] Awake — parked at {_rect.anchoredPosition}, rect.width={_rect.rect.width}, Screen.width={Screen.width}");

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        Debug.Log("[SceneSwiper] OnDestroy — unsubscribing from sceneLoaded.");
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[SceneSwiper] OnSceneLoaded '{scene.name}' — _swipedIn={_swipedIn}, pos={_rect.anchoredPosition}");
        if (_swipedIn)
            StartCoroutine(SwipeOut());
        else
            Debug.Log("[SceneSwiper] OnSceneLoaded — _swipedIn is false, skipping SwipeOut.");
    }

    private float Width() => _rect.rect.width > 0f ? _rect.rect.width : Screen.width;

    private void Park()
    {
        float w = Width();
        _rect.anchoredPosition = new Vector2(w, 0f);
        Debug.Log($"[SceneSwiper] Park — pos set to ({w}, 0)");
    }

    public IEnumerator SwipeIn()
    {
        _swipedIn = true;
        float w = Width();
        _rect.anchoredPosition = new Vector2(w, 0f);
        Debug.Log($"[SceneSwiper] SwipeIn START — from ({w},0) to (0,0), duration={SwipeInDuration}, rect.width={_rect.rect.width}");
        Play(SwipeInSound);
        yield return Slide(w, 0f, SwipeInDuration);
        Debug.Log($"[SceneSwiper] SwipeIn END — pos={_rect.anchoredPosition}");
    }

    public IEnumerator SwipeOut()
    {
        _swipedIn = false;
        // Skip one frame so Time.unscaledDeltaTime doesn't include the scene-load spike,
        // which would cause Slide to complete instantly on the first iteration.
        yield return null;
        float w = Width();
        _rect.anchoredPosition = new Vector2(0f, 0f);
        Debug.Log($"[SceneSwiper] SwipeOut START — from (0,0) to ({-w},0), duration={SwipeOutDuration}, rect.width={_rect.rect.width}");
        Play(SwipeOutSound);
        yield return Slide(0f, -w, SwipeOutDuration);
        Debug.Log($"[SceneSwiper] SwipeOut END — pos={_rect.anchoredPosition}");
        Park();
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
