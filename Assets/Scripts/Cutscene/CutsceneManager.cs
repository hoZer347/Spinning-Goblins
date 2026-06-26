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

        // Don't skip the panel if a dialogue manager is running — the click belongs to St_Dg_WaitForInput.
        var panel = Panels[CurrentPanelIndex];
        if (panel.DialogueManager != null) return;

        AdvancePanel();
    }

    public void AdvancePanel()
    {
        if (Current is St_Cs_Panel panel)
            panel.Skip();
    }
}
