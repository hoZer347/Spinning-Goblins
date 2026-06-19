using System;
using UnityEngine;


namespace AdequateGames.Dialogue
{
	[Serializable]
	public class St_Dg_StreamRichText : State<DialogueManager>
	{
		[SerializeField] public string text;

		public override void OnUpdate()
		{
			base.OnUpdate();

			Focus.TextMesh.text += text;

			Proceed();
		}
	};
};
