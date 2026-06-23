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
			// Scene isn't registered in Build Settings, so there's nothing to load. Recover cleanly
			// back to Idle rather than erroring out — add the scene under File ▸ Build Settings if
			// it's meant to be loadable.
			Debug.LogWarning($"[GameManager] Scene not in build settings, skipping transition: {Focus.PendingScenePath}");
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
