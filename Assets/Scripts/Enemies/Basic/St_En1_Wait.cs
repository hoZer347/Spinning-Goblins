using System;
using UnityEngine;

namespace hoZer
{
	[Serializable]
	public class St_En1_Wait : State<EnemyController>
	{
		Duration _waitUntilWander;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			_waitUntilWander = new Duration(
				UnityEngine.Random.Range(
					Focus.wanderWaitMin,
					Focus.wanderWaitMax));
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			if (_waitUntilWander.Tick())
				SetState<St_En1_Wander>();

			if (Vector3.Distance(
					Focus.transform.position,
					Focus.playerController.transform.position)
				<= Focus.detectionRadius)
				SetState<St_En1_Approach>();
		}
	};
};
