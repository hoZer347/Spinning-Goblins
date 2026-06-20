using hoZer;
using System;
using UnityEngine;


[Serializable]
public class St_Pl_Idle : State<PlayerController>
{
    public override void OnEnter(State lastState)
    {
        Focus.Rigidbody.bodyType = RigidbodyType2D.Kinematic;
        Focus.Rigidbody.linearVelocity = Vector2.zero;
        if (Focus.DragLine != null) Focus.DragLine.enabled = false;
    }
}
