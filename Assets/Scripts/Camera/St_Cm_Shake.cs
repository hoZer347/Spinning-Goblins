using System;
using UnityEngine;


namespace hoZer
{
	[Serializable]
	public class St_Cm_Shake : State<CameraController>
	{
		Duration shakeTime;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			shakeTime.Reset(Focus.shakeTime);
		}

		public override void OnPhysics()
		{
			base.OnPhysics();

			Focus.transform.position =
				Focus.OriginalPosition
				+ UnityEngine.Random.insideUnitSphere
				* Focus.shakeAmount;

			if (shakeTime.Tick())
				Focus.SetState<St_Cm_Idle>();
		}

		public override void OnExit(State nextState)
		{
			base.OnExit(nextState);

			Focus.transform.position = Focus.OriginalPosition;
		}
	};
};
