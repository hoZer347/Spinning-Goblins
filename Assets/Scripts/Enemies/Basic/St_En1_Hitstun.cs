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

            if (Focus.playerController != null)
            {
                Vector2 hitDir = Focus.playerController.PreImpactVelocity;
                if (hitDir.sqrMagnitude < 0.1f)
                    hitDir = (Vector2)(Focus.transform.position - Focus.playerController.transform.position);

                Focus.rigidbody.linearVelocity = hitDir.normalized * Focus.hitstunKnockback;
            }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            if (duration.Tick())
                SetState<St_En1_Stopping>();
        }
    };
};
