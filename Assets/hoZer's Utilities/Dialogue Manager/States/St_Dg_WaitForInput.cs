using System;
using UnityEngine.InputSystem;


namespace hoZer.Dialogue
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

			// Advance on a left click (how the cutscene / game is driven) or the spacebar. Guard the
			// devices: either can be null on a build with no mouse / no keyboard attached.
			bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
			bool spaced  = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

			if (clicked || spaced)
				Proceed();
		}

		public override void OnExit(State nextState)
		{
			base.OnExit(nextState);

			Focus.ContinueText.gameObject.SetActive(false);
		}
	};
};
