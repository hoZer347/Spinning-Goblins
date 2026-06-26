using hoZer;
using Tymski;
using UnityEngine;

/// <summary>
/// Drives a sequence of CutscenePanel slides then transitions to the next scene.
/// Assign panels in order in the Inspector. Set NextScene to whatever loads after.
///
/// States: St_Cs_Panel (timer per slide) → St_Cs_Complete (loads NextScene).
/// </summary>
public class CutsceneManager : StateMachine<CutsceneManager>
{
    [Header("Panels")]
    public CutscenePanel[] Panels;

    [Header("Next Scene")]
    public SceneReference NextScene;

    [Header("Skip")]
    public bool AllowSkip = false;

    public int CurrentPanelIndex { get; set; } = 0;

    protected override void OnStart()
    {
        foreach (var panel in Panels)
        {
            if (panel != null) panel.Hide();
        }

        if (Panels.Length > 0)
            SetState<St_Cs_Panel>();
        else
            SetState<St_Cs_Complete>();
    }

    protected override void OnUpdate()
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;

        if (!AllowSkip || !mouse.leftButton.wasPressedThisFrame) return;

        // Only when a panel is actually on screen. Once the last panel advances, CurrentPanelIndex is bumped
        // to Panels.Length and we hand off to St_Cs_Complete — but OnUpdate still runs for a frame, so
        // indexing here without a bounds check threw IndexOutOfRange (an empty Panels array did too).
        if (Panels == null || CurrentPanelIndex < 0 || CurrentPanelIndex >= Panels.Length) return;

        // Don't skip the panel if a dialogue manager is running — the click belongs to St_Dg_WaitForInput.
        var panel = Panels[CurrentPanelIndex];
        if (panel != null && panel.DialogueManager != null) return;

        AdvancePanel();
    }

    public void AdvancePanel()
    {
        if (Current is St_Cs_Panel panel)
            panel.Skip();
    }
}
