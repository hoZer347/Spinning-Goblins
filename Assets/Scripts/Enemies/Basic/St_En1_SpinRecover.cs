using System;
using UnityEngine;


namespace hoZer
{
	/// <summary>
	/// The wind-down after the Beeg Dwarf's spin attack: it keeps gliding in the spin direction but
	/// decelerates to a stop, while the spin sound falls in both pitch and frequency and the squash/stretch
	/// wobble eases out — then it hands back to <see cref="St_En1_Approach"/>. Still counts as "spinning"
	/// (so it repels a spinning player and sweeps neighbours), still can't fall into pits, and still takes
	/// damage if it carves into spikes.
	/// </summary>
	[Serializable]
	public class St_En1_SpinRecover : State<EnemyController>
	{
		Duration recover;
		float    fullSpeed;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			recover.Reset(Focus.spinRecoverTime);
			fullSpeed = Focus.spinAttackTime > 0f ? Focus.spinAttackDistance / Focus.spinAttackTime : 0f;

			if (Focus.rigidbody != null)
			{
				Focus.rigidbody.bodyType       = RigidbodyType2D.Kinematic;
				Focus.rigidbody.linearVelocity = Vector2.zero;
			}
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			float p         = recover.Progress; // 0 -> 1 across the wind-down
			float intensity = 1f - p;           // spin sound winds DOWN: pitch + rate fall with this

			// Lower pitch and slower whooshes as it slows (SpinSoundTick lerps both from t).
			Focus.SpinSoundTick(intensity);

			// Slow the glide to a stop.
			float speed = fullSpeed * intensity;
			Focus.MoveSafely(Focus.spinDirection * speed * Time.deltaTime, false);
			Focus.ApplySpinBounce();
			Focus.BumpNearbyEnemies();

			// Ease the wobble out as it settles.
			float wobble = Mathf.Sin(p * Mathf.PI * Focus.spinWobbleCycles) * intensity;
			float s      = wobble * Focus.spinSquash;
			Focus.transform.localScale = new Vector3(
				Focus.restScale.x * (1f + s),
				Focus.restScale.y * (1f - s),
				Focus.restScale.z);

			if (recover.Tick())
				SetState<St_En1_Approach>();
		}

		public override void OnExit(State nextState)
		{
			base.OnExit(nextState);
			Focus.transform.localScale = Focus.restScale; // never leave the body wobbled
		}
	};
};
