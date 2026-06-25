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
    const float ResetDelay       = 1.0f; // seconds the death lingers before the scene reloads
    const float MusicSlowDuration = 3.0f; // long, gradual wind-down of the music as it dies

    Duration _reset;
    bool _resetting;

    public override void OnEnter(State lastState)
    {
        Focus.Rigidbody.bodyType = RigidbodyType2D.Kinematic;
        Focus.Rigidbody.linearVelocity = Vector2.zero;

        // Caveat: if we died riiight as the Beeg Dwarf cutscene started, its dialogue box is still open.
        // Roll back the one-shot flag so the cutscene isn't lost — we'll see it next time a Beeg spawns.
        FirstTimeCutsceneTrigger.RescueIfInterrupted();

        // Wind the music down slowly — like the cutscene, but more gradual — and DON'T stop it: leave it
        // quietly playing so the DeathRestart sequence can wind it back up when the player gets going.
        if (MusicController.Instance != null)
            MusicController.Instance.SlowToStop(MusicSlowDuration, stopWhenDone: false);

        // Arm the "ready when you are" retry: the next scene load freezes everything but the player, speeds
        // the music up when they start dragging, and thaws the world the instant they fly again.
        DeathRestart.Arm();

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
