using hoZer;
using System;
using UnityEngine;


/// <summary>
/// Holds the player perfectly still and unresponsive for a cutscene — kinematic, no input, no pull,
/// and immune to contact/damage. The Beeg Dwarf intro drives the player St_Pl_Stopping (brake while the
/// music slows) → here (held through the drop and the dialogue) → St_Pl_Idle once the dialogue ends.
/// It has no OnUpdate, so it never transitions on its own; the cutscene swaps it out explicitly.
/// </summary>
[Serializable]
public class St_Pl_CutsceneHold : St_Pl_Base
{
    public override void OnEnter(State lastState)
    {
        Focus.Rigidbody.bodyType        = RigidbodyType2D.Kinematic;
        Focus.Rigidbody.linearVelocity  = Vector2.zero;
        Focus.Rigidbody.angularVelocity = 0f;

        if (Focus.DragLine != null) Focus.DragLine.enabled = false;
    }

    // Inert for the duration of the cutscene: swallow collisions and damage so nothing can knock the
    // held player around or hurt it.
    public override void OnContact(Collider2D other) { }
    public override void OnDamage(Vector2 knockbackDir = default) { }
}
