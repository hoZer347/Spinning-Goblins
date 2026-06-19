using System;
using UnityEngine;


namespace hoZer.Dialogue
{
	[Serializable]
	public class St_Dg_Begin : State<DialogueManager>
	{
		public override void OnUpdate() => Proceed();
	};
};
