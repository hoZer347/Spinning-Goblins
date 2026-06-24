using UnityEngine;

namespace hoZer
{
    /// <summary>
    /// Drives an enemy the Beeg Dwarf's landing knocked away: it flies off in a straight line while
    /// physically spinning (an actual XY rotation of the transform, not the spin sprite), and is
    /// silently destroyed the moment it clears the screen. The score for it was already awarded at the
    /// instant of impact, so the destroy gives nothing more. Runs on its own, independent of the
    /// enemy's (now frozen) StateMachine.
    /// </summary>
    public class CutsceneBounceOff : MonoBehaviour
    {
        Vector2 _velocity;
        float   _spin;       // degrees per second about Z
        Camera  _cam;

        /// <summary>Attaches the driver to <paramref name="target"/> and launches it outward along <paramref name="dir"/>.</summary>
        public static void Launch(GameObject target, Vector2 dir, float speed, float spinDegPerSec)
        {
            CutsceneBounceOff b = target.AddComponent<CutsceneBounceOff>();
            b._velocity = dir.normalized * speed;
            b._spin     = spinDegPerSec * (Random.value < 0.5f ? -1f : 1f); // tumble either way
            b._cam      = Camera.main;

            // Stop physics fighting the transform drive, and pass through everything on the way out.
            Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.bodyType = RigidbodyType2D.Kinematic; rb.linearVelocity = Vector2.zero; }

            Collider2D col = target.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }

        void Update()
        {
            transform.position += (Vector3)(_velocity * Time.deltaTime);
            transform.Rotate(0f, 0f, _spin * Time.deltaTime);

            if (OffScreen())
                Destroy(gameObject); // silent — points were already given on impact
        }

        bool OffScreen()
        {
            if (_cam == null) return true; // no camera to test against — don't linger forever

            Vector3 vp = _cam.WorldToViewportPoint(transform.position);
            const float margin = 0.2f;
            return vp.x < -margin || vp.x > 1f + margin || vp.y < -margin || vp.y > 1f + margin;
        }
    };
};
