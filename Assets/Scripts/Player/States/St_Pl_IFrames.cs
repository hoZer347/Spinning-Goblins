using hoZer;
using System;
using UnityEngine;


/// <summary>
/// Brief invulnerable window after taking damage. The player is parked, the sprite
/// blinks, and input is ignored until the i-frames elapse, then we return to Idle.
/// <see cref="PlayerController.IsInvulnerable"/> treats this state as immune to damage.
/// </summary>
[Serializable]
public class St_Pl_IFrames : State<PlayerController>
{
    private const float BlinkInterval = 0.1f;

    private float _elapsed;
    private float _blinkTimer;
    private bool _visible;

    public override void OnEnter(State lastState)
    {
        _elapsed = 0f;
        _blinkTimer = 0f;
        _visible = true;

        Focus.Rigidbody.bodyType = RigidbodyType2D.Kinematic;
        Focus.Rigidbody.linearVelocity = Vector2.zero;
        if (Focus.DragLine != null) Focus.DragLine.enabled = false;
    }

    public override void OnUpdate()
    {
        _elapsed += Time.deltaTime;

        if (Focus.Sprite != null)
        {
            _blinkTimer += Time.deltaTime;
            if (_blinkTimer >= BlinkInterval)
            {
                _blinkTimer -= BlinkInterval;
                _visible = !_visible;
                Focus.Sprite.enabled = _visible;
            }
        }

        if (_elapsed >= Focus.IFrameDuration)
            SetState<St_Pl_Idle>();
    }

    public override void OnExit(State nextState)
    {
        if (Focus.Sprite != null) Focus.Sprite.enabled = true;
    }
}
