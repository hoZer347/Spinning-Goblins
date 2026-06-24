using System.Collections;
using hoZer;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles the full exit-animation → background-load → activate sequence between any two scenes.
/// Entered by GameManager.RequestTransition(); exits back to St_Gm_Idle when complete.
///
/// The next scene is loaded asynchronously in the background with activation held off, so the load
/// overlaps the current scene's exit animation instead of stalling behind it. We only activate (swap
/// to the new scene) once the current scene is done animating out AND the load is fully prepared.
/// </summary>
public class St_Gm_Transitioning : State<GameManager>
{
	public override void OnEnter(State lastState)
	{
		Focus.StartCoroutine(TransitionRoutine());
	}

	private IEnumerator TransitionRoutine()
	{
		Debug.Log($"[St_Gm_Transitioning] Starting transition to: {Focus.PendingScenePath} | swiper={(Focus.Swiper != null ? "OK" : "NULL")} fader={(Focus.Fader != null ? "OK" : "NULL")}");

		// Begin loading the next scene in the background straight away, but hold activation so the
		// current scene stays live and visible while it loads.
		AsyncOperation load = SceneManager.LoadSceneAsync(Focus.PendingScenePath);
		if (load == null)
		{
			Debug.LogWarning($"[GameManager] Scene not in build settings, skipping transition: {Focus.PendingScenePath}");
			SetState<St_Gm_Idle>();
			yield break;
		}
		load.allowSceneActivation = false;

		// Finish off the current scene — slide the panel in / fade out to cover the screen. This runs
		// concurrently with the background load above.
		if (Focus.Swiper != null)
		{
			Debug.Log("[St_Gm_Transitioning] SwipeIn...");
			yield return Focus.StartCoroutine(Focus.Swiper.SwipeIn());
		}
		else if (Focus.Fader != null)
			yield return Focus.StartCoroutine(Focus.Fader.FadeOut());

		// Wait until the background load has finished preparing. Unity holds progress at 0.9 while
		// activation is disabled — that's our "ready to swap" signal.
		while (load.progress < 0.9f)
			yield return null;

		// Current scene is done and the next one is ready: transition to it now.
		load.allowSceneActivation = true;
		while (!load.isDone)
			yield return null;

		// SwipeOut fires automatically via SceneSwiper.OnSceneLoaded when the new scene starts.
		if (Focus.Swiper == null && Focus.Fader != null)
			yield return Focus.StartCoroutine(Focus.Fader.FadeIn());

		SetState<St_Gm_Idle>();
	}
}
