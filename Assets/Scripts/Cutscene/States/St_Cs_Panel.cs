using hoZer;

/// <summary>
/// Shows the current CutscenePanel and waits for its dialogue to call [NextPanel].
/// A DialogueManager on the panel is the only trigger to advance — there is no auto-timer.
/// Moves to the next St_Cs_Panel if more panels remain, otherwise St_Cs_Complete.
/// </summary>
public class St_Cs_Panel : State<CutsceneManager>
{
    private CutscenePanel _panel;

    public override void OnEnter(State lastState)
    {
        _panel = Focus.Panels[Focus.CurrentPanelIndex];
        _panel.Show();
    }

    public void Skip() => Advance();

    private void Advance()
    {
        Focus.CurrentPanelIndex++;

        if (Focus.CurrentPanelIndex < Focus.Panels.Length)
        {
            _panel.Hide();
            SetState<St_Cs_Panel>();
        }
        else
        {
            SetState<St_Cs_Complete>(); // leave last panel visible — scene transition handles the wipe
        }
    }

    public override void OnExit(State nextState)
    {
        if (nextState is St_Cs_Complete) return; // don't hide — scene transition handles it
        _panel?.Hide();
    }
}
