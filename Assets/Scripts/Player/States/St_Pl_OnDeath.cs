using hoZer;
using System;
using UnityEngine;


/// <summary>
/// Player death. Intentionally minimal for now — it just parks the player. Hook the real
/// game-over / respawn flow in here later.
/// </summary>
[Serializable]
public class St_Pl_OnDeath : St_Pl_Base
{
    public override void OnEnter(State lastState)
    {
        Focus.Rigidbody.bodyType = RigidbodyType2D.Kinematic;
        Focus.Rigidbody.linearVelocity = Vector2.zero;

        // TODO: death sequence / game over.
    }
}
