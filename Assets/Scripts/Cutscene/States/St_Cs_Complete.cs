using hoZer;

/// <summary>
/// Terminal cutscene state. Tells GameManager to load whatever scene comes next.
/// </summary>
public class St_Cs_Complete : State<CutsceneManager>
{
    public override void OnEnter(State lastState)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        string path = Focus.NextScene?.ScenePath;
        if (!string.IsNullOrEmpty(path))
            gm.LoadScene(Focus.NextScene);
        else
            gm.OnCutsceneComplete();
    }
}
