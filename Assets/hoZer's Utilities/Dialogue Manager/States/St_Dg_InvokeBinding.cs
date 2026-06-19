using System;
using UnityEngine;


namespace hoZer.Dialogue
{
	[Serializable]
	public class St_Dg_InvokeBinding : State<DialogueManager>
	{
		public Action binding = null;

		public override void OnUpdate()
		{
			base.OnUpdate();

			binding?.Invoke();

			Proceed();
		}
	};
};
