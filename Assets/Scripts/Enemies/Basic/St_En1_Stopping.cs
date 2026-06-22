using System;
using UnityEngine;
using UnityEngine.InputSystem;


namespace hoZer
{
	[Serializable]
	public class St_En1_Stopping : State<EnemyController>
	{
		private const float RestThreshold = 0.15f;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			Focus.rigidbody.bodyType = RigidbodyType2D.Dynamic;
		}

		public override void OnUpdate()
		{
			// Falling into a pit is handled centrally by EnemyController (IsFullyInsidePit).
			if (Focus.rigidbody.linearVelocity.magnitude < RestThreshold)
				SetState<St_En1_Approach>();
		}

		public override void OnPhysics()
		{
			// Bleed off velocity under friction until we're at rest.
			Focus.rigidbody.linearVelocity = Vector2.Lerp(
				Focus.rigidbody.linearVelocity,
				Vector2.zero,
				Focus.stopFriction * Time.fixedDeltaTime);
		}
	};
};
