using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Self-contained panel animation: fades the CanvasGroup and slides the RectTransform,
/// then staggers each direct child item in on open. No Animator required.
///
/// Setup: add this component alongside a CanvasGroup on each menu panel root.
/// The panel's RectTransform must sit inside a parent that is NOT moved by this script
/// (i.e. don't put this on the Canvas root itself).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class MenuPanel : MonoBehaviour
{
    [Header("Panel Transition")]
    public float Duration = 0.3f;
    [Tooltip("How far the panel slides in/out from its resting position.")]
    public Vector2 SlideOffset = new Vector2(80f, 0f);
    public AnimationCurve Ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Item Stagger")]
    [Tooltip("Delay between each child item animating in.")]
    public float ItemDelay = 0.05f;
    public float ItemDuration = 0.22f;
    [Tooltip("Offset items start from before sliding to their resting position.")]
    public Vector2 ItemSlideOffset = new Vector2(0f, -18f);

    private CanvasGroup _group;
    private RectTransform _rect;
    private RectTransform[] _items;
    private Vector2[] _itemOrigins;

    private void Awake() => EnsureInitialized();

    private void EnsureInitialized()
    {
        if (_group == null) _group = GetComponent<CanvasGroup>();
        if (_rect  == null) _rect  = GetComponent<RectTransform>();
        if (_items == null) CacheItems();
    }

    // Cache direct children once so AnimateIn can restore original positions.
    private void CacheItems()
    {
        var list = new List<RectTransform>();
        foreach (Transform child in transform)
        {
            var rt = child.GetComponent<RectTransform>();
            if (rt != null) list.Add(rt);
        }
        _items       = list.ToArray();
        _itemOrigins = new Vector2[_items.Length];
        for (int i = 0; i < _items.Length; i++)
            _itemOrigins[i] = _items[i].anchoredPosition;
    }

    // --- Public API ---------------------------------------------------------

    /// <summary>Fades + slides the panel in, then staggers child items.</summary>
    public IEnumerator AnimateIn()
    {
        gameObject.SetActive(true);

        // Awake may not have run if the panel started inactive — initialise now if needed.
        EnsureInitialized();

        _group.interactable    = false;
        _group.blocksRaycasts  = false;

        // Kick off item stagger immediately (they inherit alpha from the parent CanvasGroup).
        // MenuButton children do an elastic bounce-in; plain children slide in as before.
        for (int i = 0; i < _items.Length; i++)
        {
            var btn = _items[i].GetComponent<MenuButton>();
            if (btn != null)
            {
                StartCoroutine(btn.BounceIn(i * ItemDelay));
            }
            else
            {
                _items[i].anchoredPosition = _itemOrigins[i] + ItemSlideOffset;
                StartCoroutine(AnimateItem(i));
            }
        }

        // Panel fade + slide.
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.deltaTime / Duration, 1f);
            float e = Ease.Evaluate(t);
            _group.alpha          = e;
            _rect.anchoredPosition = Vector2.Lerp(SlideOffset, Vector2.zero, e);
            yield return null;
        }

        _group.alpha           = 1f;
        _rect.anchoredPosition  = Vector2.zero;
        _group.interactable    = true;
        _group.blocksRaycasts  = true;
    }

    /// <summary>Fades + slides the panel out, then deactivates it.</summary>
    public IEnumerator AnimateOut()
    {
        EnsureInitialized();
        _group.interactable   = false;
        _group.blocksRaycasts = false;

        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.deltaTime / Duration, 1f);
            float e = Ease.Evaluate(t);
            _group.alpha           = 1f - e;
            _rect.anchoredPosition = Vector2.Lerp(Vector2.zero, -SlideOffset, e);
            yield return null;
        }

        _group.alpha           = 0f;
        _rect.anchoredPosition  = Vector2.zero;
        gameObject.SetActive(false);
    }

    /// <summary>Snaps visible with no animation (used on boot for the default panel).</summary>
    public void ShowImmediate()
    {
        StopAllCoroutines();
        EnsureInitialized();
        gameObject.SetActive(true);
        _group.alpha          = 1f;
        _group.interactable   = true;
        _group.blocksRaycasts = true;
        _rect.anchoredPosition = Vector2.zero;
        for (int i = 0; i < _items.Length; i++)
        {
            _items[i].anchoredPosition = _itemOrigins[i];
            _items[i].GetComponent<MenuButton>()?.ResetScale();
        }
    }

    /// <summary>Snaps hidden with no animation.</summary>
    public void HideImmediate()
    {
        StopAllCoroutines();
        EnsureInitialized();
        _group.alpha          = 0f;
        _group.interactable   = false;
        _group.blocksRaycasts = false;
        _rect.anchoredPosition = Vector2.zero;
        gameObject.SetActive(false);
    }

    // --- Internal -----------------------------------------------------------

    private IEnumerator AnimateItem(int index)
    {
        if (index > 0)
            yield return new WaitForSeconds(index * ItemDelay);

        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.deltaTime / ItemDuration, 1f);
            float e = Ease.Evaluate(t);
            _items[index].anchoredPosition = Vector2.Lerp(
                _itemOrigins[index] + ItemSlideOffset,
                _itemOrigins[index], e);
            yield return null;
        }
        _items[index].anchoredPosition = _itemOrigins[index];
    }
}
