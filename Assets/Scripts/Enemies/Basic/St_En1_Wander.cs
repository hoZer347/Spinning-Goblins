using System;
using UnityEngine;


namespace hoZer
{
	[Serializable]
	public class St_En1_Wander : State<EnemyController>
	{
		Duration timeWandering;
		Vector2 direction;
		float wanderDistance;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			direction = UnityEngine.Random.insideUnitCircle.normalized;
			wanderDistance =
				UnityEngine.Random.Range(
					Focus.wanderDistanceMin,
					Focus.wanderDistanceMax);

			timeWandering.Reset(
				UnityEngine.Random.Range(
					Focus.wanderTimeMin,
					Focus.wanderTimeMax));
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			if (Vector3.Distance(
					Focus.transform.position,
					Focus.playerController.transform.position)
				<= Focus.detectionRadius)
				SetState<St_En1_Approach>();
		}

		public override void OnPhysics()
		{
			base.OnPhysics();

			// Wander, but turn back to Wait (re-pick a direction) if a pit/damage edge blocks us.
			if (!Focus.MoveSafely(direction * Focus.wanderSpeed * Time.fixedDeltaTime))
			{
				SetState<St_En1_Wait>();
				return;
			};

			if (Vector2.Distance(
					Focus.transform.position,
					new Vector2(
						Focus.transform.position.x,
						Focus.transform.position.y)
					+ direction
					* wanderDistance)
				>= wanderDistance)
				SetState<St_En1_Wait>();

			if (timeWandering.Tick())
				SetState<St_En1_Wait>();
		}
	};
};
