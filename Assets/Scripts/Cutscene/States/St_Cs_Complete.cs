using hoZer;

/// <summary>
/// Terminal cutscene state. Tells GameManager to load whatever scene comes next.
/// </summary>
public class St_Cs_Complete : State<CutsceneManager>
{
    public override void OnEnter(State lastState)
    {
        GameManager.Instance?.LoadScene(Focus.NextScene);
    }
}
