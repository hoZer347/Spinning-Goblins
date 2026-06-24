using System;
using UnityEngine;


namespace hoZer
{
	/// <summary>
	/// Wind-up before a fling: locks in the direction to the player and stretches the body as a
	/// telegraph, then launches. The enemy's take on the player's pull-back-then-release — slower,
	/// and while the follow-up <see cref="St_En1_Fling"/> is active it can hurt a flying player.
	/// </summary>
	[Serializable]
	public class St_En1_FlingWindup : State<EnemyController>
	{
		Duration windup;
		Vector3 baseScale;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			windup.Reset(Focus.flingWindup);
			baseScale = Focus.transform.localScale;

			// Lock the lunge direction at the start of the wind-up (a committed, dodgeable attack).
			Focus.flingDirection =
				(Focus.playerController.Position
				 - (Vector2)Focus.transform.position).normalized;
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			// Squash-and-stretch telegraph that grows as the lunge winds up.
			float t = windup.Progress;
			Focus.transform.localScale = new Vector3(
				baseScale.x * Mathf.Lerp(1f, Focus.flingStretch, t),
				baseScale.y * Mathf.Lerp(1f, 1f / Focus.flingStretch, t),
				baseScale.z);

			if (windup.Tick())
				SetState<St_En1_Fling>();
		}

		public override void OnExit(State nextState)
		{
			base.OnExit(nextState);
			Focus.transform.localScale = baseScale; // never leave the body stretched
		}
	};
};
