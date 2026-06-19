using System;
using UnityEngine;


namespace AdequateGames.Dialogue
{
	[Serializable]
	public class St_Dg_DialogueEnd : State<DialogueManager>
	{
		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			Focus.TextMesh.text = string.Empty;
		}
	};
};
