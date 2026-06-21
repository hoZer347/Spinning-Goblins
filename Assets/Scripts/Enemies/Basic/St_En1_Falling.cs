using System;
using UnityEngine;


namespace hoZer
{
	[Serializable]
	public class St_En1_Falling : State<EnemyController>
	{
		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			GameObject.Destroy(Focus.gameObject);
		}
	};
};
