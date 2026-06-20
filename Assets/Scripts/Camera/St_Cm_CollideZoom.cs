using System;
using UnityEngine;


namespace hoZer
{
	[Serializable]
	public class St_Cm_CollideZoom : State<CameraController>
	{
		public Duration zoomTime;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			zoomTime.Reset(Focus.zoomTime);
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			Focus.transform.position = Vector3.Lerp(
				Focus.transform.position,
				Focus.playerController.transform.position + new Vector3(0, 0, -10),
				Time.deltaTime * 10f);

			if (zoomTime.Tick())
				SetState<St_Cm_Idle>();
		}
	};
};
