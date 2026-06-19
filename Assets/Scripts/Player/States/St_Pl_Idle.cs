using UnityEngine;
using AdequateGames;

public class St_Pl_Idle : State<PlayerStateMachine>
{
    public override void OnEnter(State lastState)
    {
        Focus.Rigidbody.bodyType = RigidbodyType2D.Kinematic;
        Focus.Rigidbody.linearVelocity = Vector2.zero;
        if (Focus.DragLine != null) Focus.DragLine.enabled = false;
    }
}
