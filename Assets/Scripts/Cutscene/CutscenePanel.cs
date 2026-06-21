using UnityEngine;

/// <summary>
/// One slide/screen in a cutscene. CutsceneManager steps through these in order.
/// Set Duration to how long this panel should display before moving to the next one.
/// Later: add dialogue, images, animations here.
/// </summary>
public class CutscenePanel : MonoBehaviour
{
    public float Duration = 5f;

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
}
