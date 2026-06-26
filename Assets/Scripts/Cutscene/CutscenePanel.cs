using System.Collections;
using TMPro;
using hoZer.Dialogue;
using UnityEngine;

/// <summary>
/// One slide/screen in a cutscene. CutsceneManager steps through these in order.
/// Set Duration to how long this panel auto-advances when no DialogueManager is assigned.
/// If DialogueManager is assigned, the panel waits for [NextPanel] — the timer is ignored.
/// Optionally assign PanelText to get a letter-by-letter reveal animation on Show().
/// </summary>
public class CutscenePanel : MonoBehaviour
{
    public float Duration = 5f;

    [Tooltip("Optional text to reveal letter-by-letter when this panel is shown.")]
    public TMP_Text PanelText;

    [Tooltip("Seconds between each revealed character.")]
    public float LetterRevealSpeed = 0.05f;

    [Tooltip("Dialogue that starts automatically when this panel is shown. Leave empty for no dialogue.")]
    public IntroCinematicDialogueManager DialogueManager;

    private Coroutine _revealCoroutine;

    private void Awake()
    {
        // Auto-wire the dialogue manager from the same GameObject if not assigned in the Inspector.
        DialogueManager ??= GetComponent<IntroCinematicDialogueManager>();
    }

    public void Show()
    {
        gameObject.SetActive(true);

        if (PanelText != null)
        {
            if (_revealCoroutine != null) StopCoroutine(_revealCoroutine);
            _revealCoroutine = StartCoroutine(RevealText());
        }

        DialogueManager?.Begin();
    }

    public void Hide()
    {
        if (_revealCoroutine != null)
        {
            StopCoroutine(_revealCoroutine);
            _revealCoroutine = null;
        }
        gameObject.SetActive(false);
    }

    private IEnumerator RevealText()
    {
        PanelText.ForceMeshUpdate();
        int total = PanelText.textInfo.characterCount;
        PanelText.maxVisibleCharacters = 0;

        for (int i = 0; i <= total; i++)
        {
            PanelText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(LetterRevealSpeed);
        }

        _revealCoroutine = null;
    }
}
