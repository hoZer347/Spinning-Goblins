using UnityEngine;

/// <summary>
/// Row of dots above an enemy's head showing remaining health: green = intact, red = spent.
/// Built entirely in code from a generated circle sprite, so it needs no art or scene setup —
/// EnemyController spawns one on a child object and calls <see cref="SetHealth"/> on each hit.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    private static readonly Color AliveColor = new Color(0.25f, 0.9f, 0.30f);
    private static readonly Color LostColor = new Color(0.90f, 0.20f, 0.20f);

    private const float DotScale = 0.18f;   // world diameter of each dot
    private const float DotSpacing = 0.24f; // world gap between dot centers
    private const int SortingOrder = 100;   // draw on top of the enemy sprite

    private SpriteRenderer[] _dots;
    private static Sprite _square;

    private Transform _anchor; // the enemy we float above
    private float _height;

    /// <summary>Builds a bar of <paramref name="count"/> dots, floating above <paramref name="anchor"/>.</summary>
    public static EnemyHealthBar Create(Transform anchor, int count, float heightAboveCenter)
    {
        var go = new GameObject("HealthBar");
        go.transform.SetParent(anchor, false); // parented only so it's destroyed with the enemy

        var bar = go.AddComponent<EnemyHealthBar>();
        bar._anchor = anchor;
        bar._height = heightAboveCenter;
        bar.Build(count);
        bar.Follow();
        return bar;
    }

    private void LateUpdate() => Follow();

    // Float upright at a constant world size above the enemy, ignoring its rotation and scale.
    private void Follow()
    {
        if (_anchor == null) return;

        transform.position = _anchor.position + Vector3.up * _height;
        transform.rotation = Quaternion.identity;

        // Cancel the enemy's world scale so the bar (and its dots) keep a fixed on-screen size.
        Vector3 s = _anchor.lossyScale;
        transform.localScale = new Vector3(
            Mathf.Approximately(s.x, 0f) ? 1f : 1f / s.x,
            Mathf.Approximately(s.y, 0f) ? 1f : 1f / s.y,
            Mathf.Approximately(s.z, 0f) ? 1f : 1f / s.z);
    }

    private void Build(int count)
    {
        _dots = new SpriteRenderer[Mathf.Max(0, count)];
        float rowWidth = (_dots.Length - 1) * DotSpacing;

        for (int i = 0; i < _dots.Length; i++)
        {
            var dot = new GameObject($"Dot{i}");
            dot.transform.SetParent(transform, false);
            dot.transform.localPosition = new Vector3(-rowWidth * 0.5f + i * DotSpacing, 0f, 0f);
            dot.transform.localScale = Vector3.one * DotScale;

            var sr = dot.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSprite();
            sr.color = AliveColor;
            sr.sortingOrder = SortingOrder;
            _dots[i] = sr;
        }
    }

    /// <summary>Colors the first <paramref name="current"/> dots alive (green) and the rest spent (red).</summary>
    public void SetHealth(int current)
    {
        for (int i = 0; i < _dots.Length; i++)
            _dots[i].color = i < current ? AliveColor : LostColor;
    }

    // A solid white square, tinted per dot. Generated once and shared across every enemy.
    private static Sprite SquareSprite()
    {
        if (_square != null) return _square;

        const int size = 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var pixels = new Color32[size * size];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);

        tex.SetPixels32(pixels);
        tex.Apply();

        _square = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _square;
    }
}
