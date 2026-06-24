using hoZer;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;


/// <summary>
/// Player death (health depleted): park the player, then reset the level after a brief beat.
/// </summary>
[Serializable]
public class St_Pl_OnDeath : St_Pl_Base
{
    const float ResetDelay = 1.0f; // seconds the death lingers before the scene reloads

    Duration _reset;
    bool _resetting;

    public override void OnEnter(State lastState)
    {
        Focus.Rigidbody.bodyType = RigidbodyType2D.Kinematic;
        Focus.Rigidbody.linearVelocity = Vector2.zero;

        _resetting = false;
        _reset.Reset(ResetDelay);
    }

    public override void OnUpdate()
    {
        if (_resetting) return;

        if (_reset.Tick())
        {
            _resetting = true;

            // Death (no transition): GameManager sends us to a random Battle when we're in one, or
            // retries the current tutorial level. Falls back to a plain reload with no GameManager.
            var gm = GameManager.Instance;
            if (gm != null)
                gm.OnPlayerDied();
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
