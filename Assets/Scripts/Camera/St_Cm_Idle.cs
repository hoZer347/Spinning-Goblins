using System;
using UnityEngine;


namespace hoZer
{
	[Serializable]
	public class St_Cm_Idle : State<CameraController>
	{
		override public void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			Focus.transform.position = Focus.OriginalPosition;
		}
	};
};
