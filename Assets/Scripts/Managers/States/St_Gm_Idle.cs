using hoZer;

/// <summary>
/// GameManager resting state. Waits here until a scene transition is requested
/// via the GameManager public API (LoadLevel, LoadMainMenu, etc.).
/// </summary>
public class St_Gm_Idle : State<GameManager> { }
