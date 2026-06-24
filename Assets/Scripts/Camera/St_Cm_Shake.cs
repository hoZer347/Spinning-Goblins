using System;
using UnityEngine;


namespace hoZer
{
	[Serializable]
	public class St_Cm_Shake : State<CameraController>
	{
		Duration shakeTime;

		// When set, override the camera's default shake magnitude / duration for this one shake (e.g. a
		// big cutscene impact). Otherwise fall back to the serialized CameraController values.
		readonly bool  _custom;
		readonly float _amount;
		readonly float _time;

		public St_Cm_Shake() { }

		public St_Cm_Shake(float amount, float time)
		{
			_custom = true;
			_amount = amount;
			_time   = time;
		}

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			shakeTime.Reset(_custom ? _time : Focus.shakeTime);
		}

		public override void OnPhysics()
		{
			base.OnPhysics();

			var r = UnityEngine.Random.insideUnitSphere;

			Focus.transform.position =
				Focus.OriginalPosition
				+ new Vector3(r.x, r.y, 0)
				* (_custom ? _amount : Focus.shakeAmount);

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
