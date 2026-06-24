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

			// Pit kill: 100 + 20 per HP the enemy still had when it dropped in.
			ScoreUI.Instance?.AddPitKill(Focus.Health, Focus.transform.position);

			originalScale = Focus.transform.localScale;
			originalPosition = Focus.transform.position;

			timeFalling.Reset(Focus.fallingDuration);

			Focus.collider.enabled = false;

			if (Focus.audioSource != null) Focus.audioSource.PlayOneShot(Focus.pitFall, 2f);
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
