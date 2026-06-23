using hoZer;
using System;
using UnityEngine;
using UnityEngine.InputSystem;


[Serializable]
public class St_Pl_Idle : St_Pl_Base
{
    public override void OnEnter(State lastState)
    {
        Focus.Rigidbody.bodyType = RigidbodyType2D.Kinematic;
        Focus.Rigidbody.linearVelocity = Vector2.zero;
        if (Focus.DragLine != null) Focus.DragLine.enabled = false;

        // Only chain immediately if the previous state was Stopping (held through a stop).
        // Any other entry (first spawn, scene load, cutscene) requires a fresh click.
        if (lastState is St_Pl_Stopping && Mouse.current.leftButton.isPressed)
        {
            Focus.DragClickPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            SetState<St_Pl_Dragging>();
        }
    }

    public override void OnUpdate()
    {
        // Resting with our center over a pit drops us in.
        if (Focus.IsFullyInsidePit())
        {
            SetState<St_Pl_Falling>();
            return;
        }

        // Otherwise a fresh press begins a pull. Keyed on the press edge (not the held button)
        // so a click carried over from another state can't auto-drag.
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Focus.DragClickPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            SetState<St_Pl_Dragging>();
        }
    }
}
