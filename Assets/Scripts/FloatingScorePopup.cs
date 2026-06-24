using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RPG-style floating score number — the little "+30" that pops at the point of contact when you
/// score, then drifts up and fades away. Built from a legacy <see cref="TextMesh"/> (one mesh, no
/// Canvas, no TMP object) and recycled through a static pool, so a flurry of them during a long
/// combo stays cheap to spawn. Fading rides the per-object vertex colour (<see cref="TextMesh.color"/>)
/// so every popup can share one font material yet fade independently.
/// </summary>
[RequireComponent(typeof(TextMesh))]
public class FloatingScorePopup : MonoBehaviour
{
    const float Lifetime     = 0.9f;   // seconds from spawn to fully faded
    const float RiseSpeed    = 1.1f;   // world units/second of upward drift
    const float BirthPop     = 1.15f;  // brief scale-up at spawn, eased back to 1
    const int   SortingOrder = 1000;   // draw in front of the sprites

    static readonly Stack<FloatingScorePopup> _pool = new();

    TextMesh     _text;
    MeshRenderer _renderer;
    Color        _color;
    float        _age;

    /// <summary>Spawn (or recycle) a popup showing <paramref name="amount"/> at a world position.</summary>
    public static void Spawn(int amount, Vector3 worldPos, Font font, Color color)
    {
        if (font == null) return; // no font wired — skip rather than spawn invisible text

        // Pooled entries are destroyed on scene unload; skip any that Unity has torn down.
        FloatingScorePopup popup = null;
        while (popup == null && _pool.Count > 0) popup = _pool.Pop();
        if (popup == null) popup = Create(font);

        popup.Show(amount, worldPos, color);
    }

    static FloatingScorePopup Create(Font font)
    {
        var go    = new GameObject("FloatingScorePopup");
        var popup  = go.AddComponent<FloatingScorePopup>();
        popup._text     = go.GetComponent<TextMesh>();
        popup._renderer = go.GetComponent<MeshRenderer>();

        popup._text.font           = font;
        popup._renderer.sharedMaterial = font.material;
        popup._text.fontSize       = 64;     // render crisp; world size comes from characterSize
        popup._text.characterSize  = 0.06f;
        popup._text.anchor         = TextAnchor.MiddleCenter;
        popup._text.alignment      = TextAlignment.Center;
        popup._renderer.sortingOrder = SortingOrder;

        return popup;
    }

    void Show(int amount, Vector3 worldPos, Color color)
    {
        transform.position   = worldPos;
        transform.localScale = Vector3.one * BirthPop;
        _color               = color;
        _age                 = 0f;

        _text.text  = amount >= 0 ? $"+{amount}" : amount.ToString();
        _text.color = color;

        gameObject.SetActive(true);
    }

    void Update()
    {
        _age += Time.deltaTime;
        float t = _age / Lifetime;
        if (t >= 1f)
        {
            gameObject.SetActive(false);
            _pool.Push(this);
            return;
        }

        transform.position   += Vector3.up * (RiseSpeed * Time.deltaTime);   // drift upward
        transform.localScale  = Vector3.Lerp(transform.localScale, Vector3.one, 10f * Time.deltaTime);

        // Slow at first, then fall away — alpha eases out over the lifetime.
        Color c = _color;
        c.a         = 1f - t * t;
        _text.color = c;
    }
}
