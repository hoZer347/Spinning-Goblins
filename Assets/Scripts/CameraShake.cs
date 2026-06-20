using System.Collections;
using UnityEngine;

/// <summary>
/// Lightweight screen shake. Drop this on the Main Camera and call
/// <see cref="Shake(float, float)"/> from anywhere (e.g. when the player takes damage).
/// </summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private Vector3 _baseLocalPosition;
    private Coroutine _routine;

    private void Awake()
    {
        Instance = this;
        _baseLocalPosition = transform.localPosition;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Static convenience: shakes the active camera, if a CameraShake exists.</summary>
    public static void Shake(float duration, float magnitude)
    {
        if (Instance != null) Instance.ShakeInstance(duration, magnitude);
    }

    public void ShakeInstance(float duration, float magnitude)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Fade the shake out over its lifetime so it settles smoothly.
            float falloff = 1f - (elapsed / duration);
            Vector2 offset = Random.insideUnitCircle * magnitude * falloff;
            transform.localPosition = _baseLocalPosition + new Vector3(offset.x, offset.y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = _baseLocalPosition;
        _routine = null;
    }
}
