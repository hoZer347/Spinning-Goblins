using System;
using UnityEngine;


namespace hoZer
{
	/// <summary>
	/// The Beeg Dwarf's cutscene spin: it whirls in place and speeds up over a few seconds while the
	/// intro dialogue plays. Driven by the dialogue's [StartSpin] binding; the [Done] binding swaps it
	/// for the real <see cref="St_En1_SpinAttack"/> so the whirl carries straight into gameplay. Stays
	/// kinematic and stationary — only the spin sound, the squash/stretch wobble, and the sprite's whirl
	/// rate climb with the wind-up.
	/// </summary>
	[Serializable]
	public class St_En1_CutsceneSpin : State<EnemyController>
	{
		const float RampTime = 3.0f;   // seconds to wind all the way up to full speed
		const float LowHz    = 1.5f;   // wobble cycles/sec at the start
		const float HighHz   = 6.0f;   // wobble cycles/sec at full speed

		Vector3 _baseScale;
		float   _t;       // 0..1 wind-up progress
		float   _phase;   // accumulating wobble phase, so the wobble can accelerate smoothly

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			_baseScale = Focus.transform.localScale;
			_t     = 0f;
			_phase = 0f;
			Focus.ResetSpinSound();

			// Plant and hold still — kinematic so nothing can shove the spinning body around.
			if (Focus.rigidbody != null)
			{
				Focus.rigidbody.bodyType       = RigidbodyType2D.Kinematic;
				Focus.rigidbody.linearVelocity = Vector2.zero;
			}
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			_t = Mathf.Min(1f, _t + Time.deltaTime / RampTime);
			Focus.cutsceneSpinIntensity = _t; // the sprite animator reads this to speed the whirl up

			// Spin whoosh accelerates from its low idle toward a full charge.
			Focus.SpinSoundTick(_t);

			// Squash/stretch wobble that quickens and deepens as it winds up.
			_phase += Time.deltaTime * Mathf.Lerp(LowHz, HighHz, _t);
			float s = Mathf.Sin(_phase * Mathf.PI * 2f) * Focus.spinSquash * Mathf.Lerp(0.4f, 1f, _t);
			Focus.transform.localScale = new Vector3(
				_baseScale.x * (1f + s),
				_baseScale.y * (1f - s),
				_baseScale.z);
		}

		public override void OnExit(State nextState)
		{
			base.OnExit(nextState);

			Focus.transform.localScale  = _baseScale; // never leave the body wobbled
			Focus.cutsceneSpinIntensity = 0f;
		}
	};
};
