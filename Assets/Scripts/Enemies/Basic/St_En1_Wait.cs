using System;
using UnityEngine;

namespace hoZer
{
	[Serializable]
	public class St_En1_Wait : State<EnemyController>
	{
		Duration _waitUntilWander;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			// Idle on a Kinematic body so nothing can push us around while we stand still.
			Focus.EnterAIMovement();

			_waitUntilWander = new Duration(
				UnityEngine.Random.Range(
					Focus.wanderWaitMin,
					Focus.wanderWaitMax));
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			// A Beeg Dwarf charges a spin the instant the player wanders into spin-detect range.
			if (Focus.WantsSpinAttack())
			{
				SetState<St_En1_SpinWindup>();
				return;
			}

			if (_waitUntilWander.Tick())
				SetState<St_En1_Wander>();

			if (Vector2.Distance(
					Focus.transform.position,
					Focus.playerController.Position)
				<= Focus.detectionRadius)
				SetState<St_En1_Approach>();
		}
	};
};
