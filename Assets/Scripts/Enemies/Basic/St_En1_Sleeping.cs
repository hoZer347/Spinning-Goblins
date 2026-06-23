using System;
using UnityEngine;


namespace hoZer
{
	/// <summary>
	/// A dormant enemy: it does nothing — no wandering, no chasing — until the player strikes it.
	/// The hit routes through <see cref="EnemyController.TakeDamage"/> (driven by St_Pl_Base.OnContact
	/// when a flying player connects), which knocks it straight into St_En1_Hitstun; it joins the
	/// fight from there. Assign this as an enemy's start state to place a sleeping enemy. The collider
	/// stays active the whole time, so the player can always hit it awake.
	/// </summary>
	[Serializable]
	public class St_En1_Sleeping : State<EnemyController>
	{
		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			// Settle and stay put — nothing moves us until we're hit (then TakeDamage → Hitstun).
			if (Focus.rigidbody != null)
				Focus.rigidbody.linearVelocity = Vector2.zero;
		}
	};
};
