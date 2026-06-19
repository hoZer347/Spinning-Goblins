using System;
using UnityEngine.InputSystem;


namespace AdequateGames.Dialogue
{
	[Serializable]
	public class St_Dg_WaitForInput : State<DialogueManager>
	{
		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			Focus.ContinueText.gameObject.SetActive(true);
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			if (Keyboard.current.spaceKey.wasPressedThisFrame)
				Proceed();
		}

		public override void OnExit(State nextState)
		{
			base.OnExit(nextState);

			Focus.ContinueText.gameObject.SetActive(false);
		}
	};
};
