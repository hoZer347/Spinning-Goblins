using System;
using UnityEngine;


namespace hoZer
{
	/// <summary>
	/// The death throes. Entered from St_En1_Hitstun once a lethal hit's knockback slide ends: the
	/// corpse keeps whatever motion it still has (skidding to rest under the hitstun damping) while
	/// the sprite blinks, then it scores the kill and is destroyed after half a second. Pit kills
	/// never reach here — they score and vanish in St_En1_Falling instead.
	/// </summary>
	[Serializable]
	public class St_En1_Death : State<EnemyController>
	{
		const float Lifetime      = 0.5f;   // seconds of flashing before the corpse is removed
		const float FlashInterval = 0.06f;  // blink on/off period

		Duration _life;
		float    _flashTimer;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			ScoreUI.Instance?.AddKill(Focus.killScore, Focus.transform.position); // per-enemy kill score (pit kills score in St_En1_Falling)
			Focus.StopCounting(); // dead now — free its spawn slot immediately, not after the flash finishes
			_life.Reset(Lifetime);
			_flashTimer = 0f;
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			// Blink the sprite on/off as the death tell. (The body keeps sliding on its own.)
			_flashTimer += Time.deltaTime;
			if (_flashTimer >= FlashInterval)
			{
				_flashTimer -= FlashInterval;
				if (Focus.spriteRenderer != null)
					Focus.spriteRenderer.enabled = !Focus.spriteRenderer.enabled;
			}

			if (_life.Tick())
			{
				if (Focus.spriteRenderer != null) Focus.spriteRenderer.enabled = true;
				GameObject.Destroy(Focus.gameObject);
			}
		}
	};
};
