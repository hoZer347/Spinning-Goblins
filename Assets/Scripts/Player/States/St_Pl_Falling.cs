using hoZer;
using System;
using UnityEngine;


/// <summary>
/// Entered when the player is over a Pit while resting (Idle) or being pulled (Dragging).
/// The player shrinks and spins as if dropping into the pit, then — for now — respawns
/// back at the start. <see cref="PlayerController.IsInvulnerable"/> covers this state.
/// </summary>
[Serializable]
public class St_Pl_Falling : St_Pl_Base
{
    private const float SpinSpeed = 540f; // deg/sec, just for a little flourish

    private float _elapsed;
    private Vector3 _startScale;

    public override void OnEnter(State lastState)
    {
        _elapsed = 0f;
        _startScale = Focus.transform.localScale;

        Focus.Rigidbody.bodyType = RigidbodyType2D.Kinematic;
        Focus.Rigidbody.linearVelocity = Vector2.zero;
        if (Focus.DragLine != null) Focus.DragLine.enabled = false;

        if (Focus.audioSource != null) Focus.audioSource.PlayOneShot(Focus.pitFall, 2f);
    }

    public override void OnUpdate()
    {
        _elapsed += Time.deltaTime;
        float t = Focus.FallDuration > 0f ? Mathf.Clamp01(_elapsed / Focus.FallDuration) : 1f;

        Focus.transform.localScale = Vector3.Lerp(_startScale, Vector3.zero, t);
        Focus.transform.Rotate(0f, 0f, SpinSpeed * Time.deltaTime);

        if (t >= 1f)
        {
            // For now a fall just sends the player home (RespawnAtStart restores scale/rotation).
            Focus.RespawnAtStart();
            SetState<St_Pl_Idle>();
        }
    }
}
