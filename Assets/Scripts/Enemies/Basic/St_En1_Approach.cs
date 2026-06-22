using System;
using UnityEngine;


namespace hoZer
{
	[Serializable]
	public class St_En1_Approach : State<EnemyController>
	{
		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);


		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			// Fling enemies wind up a lunge once they're close enough and off cooldown.
			if (Focus.canFling
				&& Time.time >= Focus.flingReadyAt
				&& Vector2.Distance(Focus.transform.position, Focus.playerController.transform.position) <= Focus.flingRange)
			{
				SetState<St_En1_FlingWindup>();
				return;
			}

			Vector3 direction =
				(Focus.playerController.transform.position
				- Focus.transform.position)
				.normalized;

			// Chase the player, but never step onto a pit/damage tile (stops at the edge).
			Focus.MoveSafely((Vector2)direction * Focus.approachSpeed * Time.deltaTime);
		}
	};
};
