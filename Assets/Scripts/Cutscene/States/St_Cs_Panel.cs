using hoZer;

/// <summary>
/// Shows the current CutscenePanel, waits for its Duration, then advances.
/// Moves to the next St_Cs_Panel if more panels remain, otherwise St_Cs_Complete.
/// </summary>
public class St_Cs_Panel : State<CutsceneManager>
{
    private float _elapsed;
    private CutscenePanel _panel;

    public override void OnEnter(State lastState)
    {
        _elapsed = 0f;
        _panel = Focus.Panels[Focus.CurrentPanelIndex];
        _panel.Show();
    }

    public override void OnUpdate()
    {
        _elapsed += UnityEngine.Time.deltaTime;
        if (_elapsed >= _panel.Duration)
            Advance();
    }

    public void Skip() => Advance();

    private void Advance()
    {
        _panel.Hide();
        Focus.CurrentPanelIndex++;

        if (Focus.CurrentPanelIndex < Focus.Panels.Length)
            SetState<St_Cs_Panel>();
        else
            SetState<St_Cs_Complete>();
    }

    public override void OnExit(State nextState)
    {
        _panel?.Hide();
    }
}
