using System;
using UnityEngine;


namespace hoZer
{
	[Serializable]
	public class St_En1_Wander : State<EnemyController>
	{
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
		}

		public override void OnPhysics()
		{
			base.OnPhysics();

			Focus.transform.position +=
				(Vector3)(direction
					* wanderDistance
					* Time.fixedDeltaTime);
		}
	};
};
