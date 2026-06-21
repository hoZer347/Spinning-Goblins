using System.Collections;
using UnityEngine;

/// <summary>
/// Lives only in the MainMenu scene. Transitions between panels by sequencing
/// MenuPanel.AnimateOut then MenuPanel.AnimateIn — no Animator required.
///
/// Setup:
///   - Each panel root needs a CanvasGroup + MenuPanel component.
///   - Wire button OnClick() events to the public methods below.
/// </summary>
public class MenuManager : MonoBehaviour
{
    [Header("Panels")]
    public MenuPanel MainPanel;
    public MenuPanel SettingsPanel;
    public MenuPanel CreditsPanel;

    private MenuPanel _active;
    private Coroutine _transition;

    private void Start()
    {
        SettingsPanel.HideImmediate();
        CreditsPanel.HideImmediate();
        MainPanel.ShowImmediate();
        _active = MainPanel;

        StartCoroutine(MainPanel.AnimateIn());
    }

    // --- Button callbacks ---------------------------------------------------

    public void OnPlay()       => GameManager.Instance?.LoadIntro();
    public void OnSettings()   => TransitionTo(SettingsPanel);
    public void OnCredits()    => TransitionTo(CreditsPanel);
    public void OnBackToMain() => TransitionTo(MainPanel);
    public void OnQuit()       => GameManager.Instance?.QuitGame();

    // --- Internal -----------------------------------------------------------

    private void TransitionTo(MenuPanel next)
    {
        if (next == _active || _transition != null) return;
        _transition = StartCoroutine(TransitionRoutine(next));
    }

    private IEnumerator TransitionRoutine(MenuPanel next)
    {
        yield return StartCoroutine(_active.AnimateOut());
        _active = next;
        yield return StartCoroutine(_active.AnimateIn());
        _transition = null;
    }
}
