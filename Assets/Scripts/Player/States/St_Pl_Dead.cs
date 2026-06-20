using UnityEngine;
using UnityEngine.InputSystem;
using hoZer;
using System;


[Serializable]
public class St_Pl_Dead : State<PlayerStateMachine>
{
    private bool _respawning;

    public override void OnEnter(State lastState)
    {
        _respawning = false;
        Focus.Rigidbody.bodyType = RigidbodyType2D.Kinematic;
        Focus.Rigidbody.linearVelocity = Vector2.zero;
        if (Focus.DeathPanel != null) Focus.DeathPanel.SetActive(true);
    }

    public override void OnUpdate()
    {
        if (_respawning) return;

        bool clicked = Mouse.current.leftButton.wasPressedThisFrame;
        bool spaced = Keyboard.current.spaceKey.wasPressedThisFrame;

        if (clicked || spaced) Respawn();
    }

    // Called by the respawn button's UnityEvent in the Inspector as well
    public void Respawn()
    {
        if (_respawning) return;
        _respawning = true;

        if (Focus.DeathPanel != null) Focus.DeathPanel.SetActive(false);
        Focus.RespawnAtStart();
        SetState<St_Pl_Idle>();
    }

    public override void OnExit(State nextState)
    {
        if (Focus.DeathPanel != null) Focus.DeathPanel.SetActive(false);
    }
}
