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
		Debug.Log($"[St_Gm_Transitioning] Starting transition to: {Focus.PendingScenePath} | swiper={(Focus.Swiper != null ? "OK" : "NULL")} fader={(Focus.Fader != null ? "OK" : "NULL")}");

		// Slide panel in to cover the screen before the load.
		if (Focus.Swiper != null)
		{
			Debug.Log("[St_Gm_Transitioning] SwipeIn...");
			yield return Focus.StartCoroutine(Focus.Swiper.SwipeIn());
		}
		else if (Focus.Fader != null)
			yield return Focus.StartCoroutine(Focus.Fader.FadeOut());

		AsyncOperation load = SceneManager.LoadSceneAsync(Focus.PendingScenePath);
		if (load == null)
		{
			Debug.LogWarning($"[GameManager] Scene not in build settings, skipping transition: {Focus.PendingScenePath}");
			if (Focus.Swiper != null)
				yield return Focus.StartCoroutine(Focus.Swiper.SwipeOut());
			else if (Focus.Fader != null)
				yield return Focus.StartCoroutine(Focus.Fader.FadeIn());
			SetState<St_Gm_Idle>();
			yield break;
		}
		while (!load.isDone)
			yield return null;

		// SwipeOut fires automatically via SceneSwiper.OnSceneLoaded when the new scene starts.
		if (Focus.Swiper == null && Focus.Fader != null)
			yield return Focus.StartCoroutine(Focus.Fader.FadeIn());

		SetState<St_Gm_Idle>();
	}
}
