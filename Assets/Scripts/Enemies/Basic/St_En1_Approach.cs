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

			Vector3 direction =
				(Focus.playerController.transform.position
				- Focus.transform.position)
				.normalized;

			Focus.transform.position
				+= direction
				* Focus.approachSpeed
				* Time.deltaTime;
		}
	};
};
