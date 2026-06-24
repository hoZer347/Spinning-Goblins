using System;
using UnityEngine;


namespace hoZer
{
	[Serializable]
	public class St_En1_Approach : State<EnemyController>
	{
		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			// Walk by transform on a Kinematic body so the player can't shove us off course
			// (and leave us drifting) when it bounces back after a hit.
			Focus.EnterAIMovement();
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			// Beeg Dwarves charge a spin the moment the player is within spin-detect range and off cooldown.
			// The spin is their ONLY close-range attack — it takes over from here straight into the wind-up.
			if (Focus.WantsSpinAttack())
			{
				SetState<St_En1_SpinWindup>();
				return;
			}

			// Fling enemies wind up a lunge once they're close enough and off cooldown. A spin-capable
			// dwarf never flings — otherwise the longer flingRange fires a lunge before it's close
			// enough to spin, which reads as a stray forward dash between the charge and the spin.
			if (Focus.canFling
				&& !Focus.canSpinAttack
				&& Time.time >= Focus.flingReadyAt
				&& Vector2.Distance(Focus.transform.position, Focus.playerController.Position) <= Focus.flingRange)
			{
				SetState<St_En1_FlingWindup>();
				return;
			}

			Vector2 direction =
				(Focus.playerController.Position
				- (Vector2)Focus.transform.position)
				.normalized;

			// Chase the player, but never step onto a pit/damage tile (stops at the edge).
			Focus.MoveSafely(direction * Focus.approachSpeed * Time.deltaTime);
		}
	};
};
