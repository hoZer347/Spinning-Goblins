using System;
using UnityEngine;


namespace hoZer
{
	/// <summary>
	/// The Beeg Dwarf's charge-up before a spin attack. It plants and winds up a low-pitched spin —
	/// the same build-up cadence as the player's pull-back, only slower and an octave down. The dwarf
	/// is fully vulnerable the whole time: a player hit routes through TakeDamage straight into
	/// St_En1_Hitstun, just like any other unit. When the charge completes it releases into
	/// <see cref="St_En1_SpinAttack"/>.
	/// </summary>
	[Serializable]
	public class St_En1_SpinWindup : State<EnemyController>
	{
		Duration windup;
		Vector3 baseScale;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			windup.Reset(Focus.spinWindup);
			baseScale = Focus.transform.localScale;
			Focus.ResetSpinSound();

			// Plant and hold still while charging — kinematic so nothing shoves the charging body
			// around. A hit still routes through TakeDamage → Hitstun (which flips it Dynamic), so it
			// stays as catchable as a regular unit during the wind-up.
			if (Focus.rigidbody != null)
			{
				Focus.rigidbody.bodyType       = RigidbodyType2D.Kinematic;
				Focus.rigidbody.linearVelocity = Vector2.zero;
			}
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			float t = windup.Progress;

			// Spin sound winds up like the player's: the rate and the (low) pitch climb with the charge.
			Focus.SpinSoundTick(t);

			// A squash/stretch wobble that grows with the charge telegraphs the coming attack.
			float wobble = Mathf.Sin(t * Mathf.PI * Focus.spinWobbleCycles) * t;
			float s = wobble * Focus.spinSquash;
			Focus.transform.localScale = new Vector3(
				baseScale.x * (1f + s),
				baseScale.y * (1f - s),
				baseScale.z);

			if (windup.Tick())
				SetState<St_En1_SpinAttack>();
		}

		public override void OnExit(State nextState)
		{
			base.OnExit(nextState);
			Focus.transform.localScale = baseScale; // never leave the body wobbled
		}
	};
};
