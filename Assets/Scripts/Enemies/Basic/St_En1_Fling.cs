using System;
using UnityEngine;


namespace hoZer
{
	/// <summary>
	/// The committed lunge: launches along the locked-in direction at flingSpeed (slower than the
	/// player), bleeds off speed, then settles via St_En1_Stopping back into the approach loop.
	/// While in this state the enemy damages a flying player instead of being damaged by it
	/// (see St_Pl_Base). A cooldown is armed here so the enemy chases a bit between lunges.
	/// </summary>
	[Serializable]
	public class St_En1_Fling : State<EnemyController>
	{
		private const float RestThreshold = 0.5f;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			Focus.rigidbody.bodyType = RigidbodyType2D.Dynamic;
			Focus.rigidbody.linearVelocity = Focus.flingDirection * Focus.flingSpeed;
			Focus.flingReadyAt = Time.time + Focus.flingCooldown;

			SfxManager.Play(Focus.hitstunSound, 0.3f);
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			if (Focus.rigidbody.linearVelocity.magnitude < RestThreshold)
				SetState<St_En1_Stopping>();
		}

		public override void OnPhysics()
		{
			base.OnPhysics();

			// Never lunge into a pit by ourselves — kill the velocity at the edge and settle.
			if (Focus.PitAhead())
			{
				Focus.rigidbody.linearVelocity = Vector2.zero;
				SetState<St_En1_Stopping>();
				return;
			}

			// Bleed off speed so the lunge has a limited range; St_En1_Stopping finishes the settle.
			Focus.rigidbody.linearVelocity = Vector2.Lerp(
				Focus.rigidbody.linearVelocity,
				Vector2.zero,
				Focus.stopFriction * Time.fixedDeltaTime);
		}
	};
};
