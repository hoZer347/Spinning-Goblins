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
		if (enemyController != null
			&& Focus.Current is St_Pl_Flying)
			enemyController.SetState<St_En1_Hitstun>();

		if (Focus.IsDamageLayer(other.gameObject.layer))
            OnDamage();
    }

    /// <summary>
    /// Take a hit: shake the camera, respawn at the start, then enter i-frames — unless the
    /// current state is already invulnerable. Also the entry point for external damage sources
    /// via <see cref="PlayerController.TakeDamage"/>.
    /// </summary>
    public virtual void OnDamage()
    {
        if (Focus.IsInvulnerable) return;

        // TODO: subtract from a health pool here, only respawn / go to St_Pl_Dead when depleted.
        Focus.ShakeCamera();
        Focus.RespawnAtStart();
        SetState<St_Pl_IFrames>();
    }
}
