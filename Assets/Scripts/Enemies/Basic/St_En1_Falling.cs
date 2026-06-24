using System;
using UnityEngine;


namespace hoZer
{
	[Serializable]
	public class St_En1_Falling : State<EnemyController>
	{
		Duration timeFalling;
		Vector3 originalScale;
		Vector3 originalPosition;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			// Pit kill: the enemy's kill score + 20 per HP it still had when it dropped in.
			ScoreUI.Instance?.AddPitKill(Focus.killScore, Focus.Health, Focus.transform.position);
			Focus.StopCounting(); // dropping in — free its spawn slot immediately, not after the fall finishes

			originalScale = Focus.transform.localScale;
			originalPosition = Focus.transform.position;

			timeFalling.Reset(Focus.fallingDuration);

			Focus.collider.enabled = false;

			// Through the SfxManager so a cluster of enemies dropping at once doesn't blow out the mix.
			SfxManager.Play(Focus.pitFall, 2f);
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			float progress = timeFalling.Progress;

			Focus.transform.localScale =
				Vector3.Lerp(
					originalScale,
					Vector3.zero,
					progress);

			Focus.transform.position
				= originalPosition;

			if (timeFalling.Tick())
				GameObject.Destroy(Focus.gameObject);
		}
	};
};
