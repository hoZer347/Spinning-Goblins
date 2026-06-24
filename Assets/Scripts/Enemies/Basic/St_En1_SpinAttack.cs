using System;
using UnityEngine;


namespace hoZer
{
	/// <summary>
	/// The Beeg Dwarf's released spin attack. It glides a long way across the arena (set by
	/// spinAttackDistance / spinAttackTime), squashing and stretching as it goes. Any enemy it sweeps
	/// through is bounced into hitstun without the dwarf being deflected (it moves on a script-driven
	/// kinematic path, so contacts never push it). While spinning it REPELS a spinning player unharmed
	/// (St_Pl_Base) — the wind-up is the window to punish it — but a player's strike still kicks it back a
	/// little (the spin bounce). It can't fall into pits (it carves over them), but carving into spikes
	/// hurts it. When the glide finishes it hands off to <see cref="St_En1_SpinRecover"/>, which slows it
	/// to a stop while the spin sound winds down.
	/// </summary>
	[Serializable]
	public class St_En1_SpinAttack : State<EnemyController>
	{
		Duration attack;
		Vector2 direction;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			attack.Reset(Focus.spinAttackTime);
			Focus.spinReadyAt = Time.time + Focus.spinCooldown;

			// Commit the glide direction toward the player at the moment the spin releases.
			direction = (Focus.playerController.Position
					   - (Vector2)Focus.transform.position).normalized;
			Focus.spinDirection = direction;

			// Script-driven glide: stay kinematic so bumped enemies never deflect our trajectory.
			if (Focus.rigidbody != null)
			{
				Focus.rigidbody.bodyType       = RigidbodyType2D.Kinematic;
				Focus.rigidbody.linearVelocity = Vector2.zero;
			}
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			float t = attack.Progress;

			// Hold the low spin at full rate through the whole attack.
			Focus.SpinSoundTick(1f);

			// Squash/stretch wobble across the spin — the player's spin feel, just slowed down.
			float wobble = Mathf.Sin(t * Mathf.PI * Focus.spinWobbleCycles);
			float s      = wobble * Focus.spinSquash;
			Focus.transform.localScale = new Vector3(
				Focus.restScale.x * (1f + s),
				Focus.restScale.y * (1f - s),
				Focus.restScale.z);

			// Glide across the whole attack. avoidDamage:false lets it carve INTO spikes (which hurts it —
			// handled centrally in EnemyController.OnPhysics) while it still refuses to walk into a pit.
			float speed = Focus.spinAttackTime > 0f ? Focus.spinAttackDistance / Focus.spinAttackTime : 0f;
			Focus.MoveSafely(direction * speed * Time.deltaTime, false);
			Focus.ApplySpinBounce();

			Focus.BumpNearbyEnemies();

			if (attack.Tick())
				SetState<St_En1_SpinRecover>();
		}

		public override void OnExit(State nextState)
		{
			base.OnExit(nextState);
			Focus.transform.localScale = Focus.restScale; // never leave the body wobbled
		}
	};
};
