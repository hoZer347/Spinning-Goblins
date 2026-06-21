using System.Collections;
using hoZer;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles the full fade-out → async load → fade-in sequence between any two scenes.
/// Entered by GameManager.RequestTransition(); exits back to St_Gm_Idle when complete.
/// </summary>
public class St_Gm_Transitioning : State<GameManager>
{
    public override void OnEnter(State lastState)
    {
        Focus.StartCoroutine(TransitionRoutine());
    }

    private IEnumerator TransitionRoutine()
    {
        if (Focus.Fader != null)
            yield return Focus.StartCoroutine(Focus.Fader.FadeOut());

        AsyncOperation load = SceneManager.LoadSceneAsync(Focus.PendingScenePath);
        if (load == null)
        {
            Debug.LogError($"[GameManager] Scene not in build settings: {Focus.PendingScenePath}");
            if (Focus.Fader != null)
                yield return Focus.StartCoroutine(Focus.Fader.FadeIn());
            SetState<St_Gm_Idle>();
            yield break;
        }
        while (!load.isDone)
            yield return null;

        if (Focus.Fader != null)
            yield return Focus.StartCoroutine(Focus.Fader.FadeIn());

        SetState<St_Gm_Idle>();
    }
}
