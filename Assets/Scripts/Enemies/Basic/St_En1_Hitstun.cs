using System;
using UnityEngine;


namespace hoZer
{
	[Serializable]
	public class St_En1_Hitstun : State<EnemyController>
	{
		Duration duration;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			duration.Reset(Focus.hitstunDuration);
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			if (duration.Tick())
				SetState<St_En1_Stopping>();
		}
	};
};
