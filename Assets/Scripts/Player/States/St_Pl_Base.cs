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
			if (Focus.Current is St_Pl_Flying)
			{
				// A flinging enemy's lunge beats the player's flight: the player gets hurt instead
				// of the enemy. Any other time, the flying player damages the enemy as usual.
				if (enemyController.Current is St_En1_Fling)
				{
					Vector2 c = Focus.Collider.bounds.center;
					OnDamage(c - (Vector2)enemyController.transform.position);
				}
				else
				{
					Focus.ShakeCamera();
					enemyController.TakeDamage();   // spend a dot; hitstun on survive, die when depleted
					Focus.audioSource.PlayOneShot(Focus.hit, .3f);
				}
			};

			if (Focus.Current is St_Pl_Stopping
				|| Focus.Current is St_Pl_Dragging
				|| Focus.Current is St_Pl_Idle)
			{
				Focus.ShakeCamera();
				Focus.audioSource.PlayOneShot(Focus.gobHurt, .2f);
				Focus.audioSource.PlayOneShot(Focus.hit, .3f);

				Vector3 direction =
					(enemyController.transform.position
					 - Focus.transform.position).normalized;

				// A bodyType change inside a collision callback is DEFERRED until after the
				// physics step, so the body is still Kinematic this frame and AddForce gets
				// dropped (forces are ignored on Kinematic bodies). Setting linearVelocity IS
				// honoured on a Kinematic body and is preserved when it flips to Dynamic — so
				// the knockback actually lands.
				Focus.Rigidbody.bodyType = RigidbodyType2D.Dynamic;
				Focus.Rigidbody.linearVelocity = -direction * Focus.HurtBounceForce;

				// Pause the enemy briefly so it stops shoving the player while we recover.
				enemyController.SetState<St_En1_Pause>();

				// Spend a dot if the player uses a health bar; an empty bar means death.
				if (Focus.SpendHealth())
				{
					SetState<St_Pl_OnDeath>();
					return;
				}
				SetState<St_Pl_Hitstun>();
			};
		};

		// Impact sound when bouncing off a wall mid-flight.
		if (Focus.Current is St_Pl_Flying && Focus.IsObstacleLayer(other.gameObject.layer))
			Focus.audioSource.PlayOneShot(Focus.hit, .3f);

		if (Focus.IsDamageLayer(other.gameObject.layer))
		{
			// Knock the player straight away from the spike surface it touched.
			Vector2 center = Focus.Collider.bounds.center;
			OnDamage(center - (Vector2)other.ClosestPoint(center));
		}
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

        // Spikes / damage impact.
        Focus.audioSource.PlayOneShot(Focus.hit, .3f);
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
