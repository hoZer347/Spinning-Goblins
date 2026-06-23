using System;
using UnityEngine;


namespace hoZer
{
	[Serializable]
	public class St_En1_Die : State<EnemyController>
	{
		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			ScoreUI.Instance?.AddKill(); // 100 for a normal kill (pit kills score in St_En1_Falling)
			GameObject.Destroy(Focus.gameObject);
		}
	};
};
