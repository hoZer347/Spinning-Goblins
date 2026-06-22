using System;
using UnityEngine;
using UnityEngine.InputSystem;


namespace hoZer
{
	/// <summary>
	/// Active braking: pressing while Flying enters this state to kill momentum quickly. The
	/// player decelerates under stopFriction, then settles into Idle. A fresh press cancels the
	/// stop straight into a new pull.
	/// </summary>
	[Serializable]
	public class St_Pl_Stopping : St_Pl_Base
	{
		private const float RestThreshold = 0.15f;

		public override void OnEnter(State lastState)
		{
			Focus.Rigidbody.bodyType = RigidbodyType2D.Dynamic;
		}

		public override void OnUpdate()
		{
			if (Focus.IsFullyInsidePit())
			{
				SetState<St_Pl_Falling>();
				return;
			};

			// A *fresh* press cancels the stop into a new pull (or a pit drop). Reacting to the
			// press edge rather than the held button is what stops the click that began braking
			// from instantly turning into a drag.
			if (Mouse.current.leftButton.wasPressedThisFrame)
			{
				Focus.DragClickPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
				SetState<St_Pl_Dragging>();
				return;
			}

			if (Focus.Rigidbody.linearVelocity.magnitude < RestThreshold)
				SetState<St_Pl_Idle>();
		}

		public override void OnPhysics()
		{
			// Bleed off velocity under friction until we're at rest.
			Focus.Rigidbody.linearVelocity = Vector2.Lerp(
				Focus.Rigidbody.linearVelocity,
				Vector2.zero,
				Focus.stopFriction * Time.fixedDeltaTime);
		}
	};
};
