using hoZer;
using UnityEngine;


/// <summary>
/// Base for every player state. Centralises the cross-cutting transitions that used to live
/// on the controller — taking damage and beginning a pull — so the machine itself never calls
/// SetState. Every transition is owned by a state.
/// </summary>
public abstract class St_Pl_Base : State<PlayerController>
{
    /// <summary>
    /// A collision/trigger the controller forwarded to the current state. Damage tiles knock
    /// the player back to spawn and into i-frames.
    /// </summary>
    public virtual void OnContact(Collider2D other)
    {
		// A flying player stuns the enemy it strikes. Drive the ENEMY's machine into hitstun —
		// calling the bare SetState here would push an EnemyController state onto the player's
		// own machine, where Focus (stateMachine as EnemyController) is null.
		EnemyController enemyController = other.GetComponent<EnemyController>();
		if (enemyController != null)
		{
			// A reeling, dying or falling enemy is harmless — it can never damage the player, whether
			// the player is flying or resting. Only a live, active enemy is a threat.
			bool enemyHarmless = enemyController.Current is St_En1_Hitstun
				|| enemyController.Current is St_En1_Death
				|| enemyController.Current is St_En1_Falling;

			if (Focus.Current is St_Pl_Flying)
			{
				// The Beeg Dwarf's released spin clashes with the player's own: a spinning (flying)
				// player is bounced away UNHARMED — no health lost, no hitstun — instead of taking a
				// hit. This repel happens only while the player is spinning (i.e. in this Flying branch),
				// for the whole spin (attack + its wind-down).
				if (!enemyHarmless && enemyController.IsSpinning)
				{
					BounceOffEnemySpin(enemyController);
				}
				// A fling lunge still beats the player's flight: the player gets hurt instead of the enemy.
				else if (!enemyHarmless && enemyController.IsDealingContactDamage)
				{
					Vector2 c = Focus.Collider.bounds.center;
					OnDamage(c - (Vector2)enemyController.transform.position);
				}
				else
				{
					Focus.ShakeCamera();
					enemyController.TakeDamage();   // spend a dot; hitstun on survive, die when depleted
					// 10, then +10 per consecutive hit this flight — popped at the point of contact.
					Vector2 contact = enemyController.collider != null
						? enemyController.collider.ClosestPoint(Focus.Collider.bounds.center)
						: (Vector2)enemyController.transform.position;
					ScoreUI.Instance?.AddFlyingHit(contact);
					SfxManager.Play(Focus.hit, .3f); // throttled so a chain of hits doesn't stack into a roar

					// Hitting an enemy shouldn't recoil the player as hard — the enemy is the one sent
					// reeling. Trim the just-resolved bounce a touch back toward the incoming heading.
					Focus.Rigidbody.linearVelocity = Vector2.Lerp(
						Focus.Rigidbody.linearVelocity,
						Focus.PreImpactVelocity,
						Focus.EnemyHitBounceReduction);
				}
			};

			// Resting / braking: a live enemy walking into the player knocks it into hitstun. Dragging
			// is handled separately in St_Pl_Dragging by an explicit overlap at the goblin's on-screen
			// position — while the body is Kinematic and hand-positioned, the physics collider can lag
			// at the drag origin, so an OnContact hit here would fire back there, not on the model.
			if (!enemyHarmless &&
				(Focus.Current is St_Pl_Stopping
				|| Focus.Current is St_Pl_Idle))
			{
				HurtFromEnemy(enemyController);
			};
		};

		// Impact sound when bouncing off a wall mid-flight.
		if (Focus.Current is St_Pl_Flying && Focus.IsObstacleLayer(other.gameObject.layer))
			SfxManager.Play(Focus.hit, .3f);

		if (Focus.IsDamageLayer(other.gameObject.layer))
		{
			// Knock the player straight away from the spike surface it touched.
			Vector2 center = Focus.Collider.bounds.center;
			OnDamage(center - (Vector2)other.ClosestPoint(center));
		}
    }

    /// <summary>
    /// The Beeg Dwarf's spin repels the spinning player rather than hurting it: fling the player away
    /// from the dwarf — at its incoming speed, or a floor bounce, whichever is greater — with no damage,
    /// no health spent, and no hitstun. The player stays in its flight, just redirected outward.
    /// </summary>
    void BounceOffEnemySpin(EnemyController enemy)
    {
        Vector2 away = (Vector2)Focus.Collider.bounds.center - (Vector2)enemy.transform.position;
        if (away.sqrMagnitude < 0.0001f) away = -Focus.PreImpactVelocity; // dead-centre hit: reverse course
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up;

        float speed = Mathf.Max(Focus.PreImpactVelocity.magnitude, Focus.HurtBounceForce);
        Focus.Rigidbody.linearVelocity = away * speed;

        // ...and kick the dwarf back a little too, so the clash recoils both ways.
        enemy.AddSpinBounce(Focus.Collider.bounds.center);

        SfxManager.Play(Focus.hit, .3f); // impact feedback only — the player is unharmed
    }

    /// <summary>
    /// A live enemy knocks the resting / aiming player back into hitstun: bounce away from the enemy,
    /// briefly pause it so it stops shoving, then spend a health dot (death when the bar empties).
    /// Shared by enemy contact while Idle / Stopping and the tight proximity check while Dragging.
    /// </summary>
    protected void HurtFromEnemy(EnemyController enemyController)
    {
        Focus.ShakeCamera();
        SfxManager.Play(Focus.gobHurt, .5f);
        SfxManager.Play(Focus.hit, .3f);

        // From the goblin's drawn position (the sprite), not the root — during a drag the root sits
        // back at the origin, so using it would knock the player off along the wrong line.
        Vector2 direction =
            ((Vector2)enemyController.transform.position - Focus.Position).normalized;

        // A bodyType change inside a collision callback is DEFERRED until after the physics step, so
        // the body is still Kinematic this frame and AddForce gets dropped (forces are ignored on
        // Kinematic bodies). Setting linearVelocity IS honoured on a Kinematic body and is preserved
        // when it flips to Dynamic — so the knockback actually lands.
        Focus.Rigidbody.bodyType = RigidbodyType2D.Dynamic;
        Focus.Rigidbody.linearVelocity = -direction * Focus.HurtBounceForce;

        // Pause the enemy briefly so it stops shoving the player while we recover — but never yank one
        // out of its hit/death/fall sequence. Pausing a 0-HP enemy mid-hitstun (or a flashing corpse)
        // would skip the Hitstun→Death routing and resurrect it into the wander/approach loop, alive
        // at 0 HP. Only pause an enemy that's still in the fight.
        if (enemyController.Health > 0
            && !(enemyController.Current is St_En1_Hitstun
                || enemyController.Current is St_En1_Death
                || enemyController.Current is St_En1_Falling
                || enemyController.Current is St_En1_SpinWindup
                || enemyController.Current is St_En1_SpinAttack
                || enemyController.Current is St_En1_SpinRecover))
            enemyController.SetState<St_En1_Pause>();

        // Spend a dot if the player uses a health bar; an empty bar means death.
        if (Focus.SpendHealth())
        {
            SetState<St_Pl_OnDeath>();
            return;
        }
        SetState<St_Pl_Hitstun>();
    }

    /// <summary>
    /// Take a hit from spikes / a damage source: shake, then drop into hitstun — no reset. The
    /// player bounces off the spike (physics material) and recovers in place. Pits are handled
    /// separately by St_Pl_Falling, which still drops in and respawns. Skipped while already
    /// invulnerable. Also the entry point for external damage via <see cref="PlayerController.TakeDamage"/>.
    /// </summary>
    public virtual void OnDamage(Vector2 knockbackDir = default)
    {
        if (Focus.IsInvulnerable) return;

        SfxManager.Play(Focus.hit, .3f);
        Focus.ShakeCamera();

        // Spend a dot if the player uses a health bar; an empty bar means death.
        if (Focus.SpendHealth())
        {
            SetState<St_Pl_OnDeath>();
            return;
        }

        // Launch the player off at a fixed speed (no reset). Prefer the away-from-spike direction
        // the caller computed; fall back to the current heading for directionless hits.
        Vector2 dir = knockbackDir.sqrMagnitude > 0.0001f
            ? knockbackDir.normalized
            : Focus.Rigidbody.linearVelocity.normalized;

        // Stay Dynamic so the launch carries the player away through the stun, instead of
        // teleporting back to spawn the way the old reset did.
        Focus.Rigidbody.bodyType = RigidbodyType2D.Dynamic;
        Focus.Rigidbody.linearVelocity = dir * Focus.SpikeBounceSpeed;
        SetState<St_Pl_Hitstun>();
    }
}
