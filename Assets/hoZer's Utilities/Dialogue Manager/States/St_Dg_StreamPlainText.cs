using System;
using UnityEngine;
using UnityEngine.InputSystem;


namespace hoZer.Dialogue
{
	[Serializable]
	public class St_Dg_StreamPlainText : State<DialogueManager>
	{
		[SerializeField] public string text;
		Duration duration;
		int amountStreamed = 0;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			duration.Reset(Focus.textSpeed);
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			if (duration.Tick())
			{
				duration.Reset(Focus.textSpeed);

				char c = text[amountStreamed++];
				Focus.TextMesh.text += c;
				Focus.Speak(c); // Banjo-Kazooie-style pitched blip per character
			};

			if (Keyboard.current.spaceKey.wasPressedThisFrame)
			{
				Focus.TextMesh.text += text.Substring(amountStreamed);

				Proceed();

				return;
			};

			if (amountStreamed >= text.Length)
				Proceed();
		}
	};
};
