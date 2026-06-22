using System;
using UnityEngine;


namespace hoZer
{
	/// <summary>
	/// Brief stop after bumping the player. The enemy holds still for pauseDuration so it isn't
	/// constantly shoving against the PlayerController, then resumes wandering (which re-engages
	/// Approach if the player is still in range).
	/// </summary>
	[Serializable]
	public class St_En1_Pause : State<EnemyController>
	{
		Duration pause;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			pause.Reset(Focus.pauseDuration);

			// Stop dead so we don't keep pushing the player during the pause.
			if (Focus.rigidbody != null)
				Focus.rigidbody.linearVelocity = Vector2.zero;
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			if (pause.Tick())
				SetState<St_En1_Wander>();
		}
	};
};
