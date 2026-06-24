using System;
using UnityEngine;


namespace hoZer
{
    [Serializable]
    public class St_En1_Hitstun : State<EnemyController>
    {
        Duration duration;

        private static readonly PhysicsMaterial2D _bounceMat = new PhysicsMaterial2D("EnemyBounce")
        {
            friction   = 0f,
            bounciness = 0.8f,
        };

        public override void OnEnter(State lastState)
        {
            base.OnEnter(lastState);
            duration.Reset(Focus.hitstunDuration);

            Focus.rigidbody.bodyType       = RigidbodyType2D.Dynamic;
            Focus.rigidbody.sharedMaterial = _bounceMat;
            Focus.rigidbody.linearDamping  = 1f;

            // An external bumper (the Beeg Dwarf's spin) gets first say on the reel direction, so the
            // victim flies away from what actually hit it rather than from the player.
            if (Focus.hasExternalKnockback)
            {
                Focus.rigidbody.linearVelocity = Focus.externalKnockbackDir.normalized * Focus.hitstunKnockback;
                Focus.hasExternalKnockback = false;
            }
            else if (Focus.playerController != null)
            {
                Vector2 hitDir = Focus.playerController.PreImpactVelocity;
                if (hitDir.sqrMagnitude < 0.1f)
                    hitDir = (Vector2)Focus.transform.position - Focus.playerController.Position;

                Focus.rigidbody.linearVelocity = hitDir.normalized * Focus.hitstunKnockback;
            }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            if (duration.Tick())
            {
                if (Focus.Health <= 0)
                    SetState<St_En1_Death>();    // dead: skid done, now flash out and destroy
                else
                    SetState<St_En1_Stopping>(); // survived: settle back into the fight
            }
        }
    };
};
