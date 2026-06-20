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
			if (Focus.IsCenterOverPit())
			{
				Focus.gameObject.SetActive(false);
				gameObject.SetActive(false);
				return;
			};

			if (Focus.rigidbody.linearVelocity.magnitude < RestThreshold)
				SetState<St_En1_Wait>();
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
