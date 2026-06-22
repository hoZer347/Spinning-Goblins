using System;
using UnityEngine;
using hoZer;
using UnityEngine.InputSystem;


[Serializable]
public class St_Pl_Flying : St_Pl_Base
{
    private const float RestThreshold = 0.15f;
    private const float MinFlightTime = 0.25f;

    private float _elapsed;

    public override void OnEnter(State lastState)
    {
        _elapsed = 0f;
        Focus.Rigidbody.bodyType = RigidbodyType2D.Dynamic;
        Focus.Rigidbody.linearDamping = Focus.LinearDamping;
        Focus.Rigidbody.AddForce(Focus.LaunchForce, ForceMode2D.Impulse);
    }

    public override void OnUpdate()
    {
        // A press brakes the flight.
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            SetState<St_Pl_Stopping>();
            return;
        }

        // Spin stays high while we're moving fast and only drops off near the end of the flight.
        Focus.SpinFlightTick(Focus.Rigidbody.linearVelocity.magnitude);

        // Otherwise settle into Idle once we've slowed down.
        _elapsed += Time.deltaTime;
        if (_elapsed >= MinFlightTime && Focus.Rigidbody.linearVelocity.magnitude < RestThreshold)
            SetState<St_Pl_Idle>();
    }

    public override void OnPhysics()
    {
        ApplyStickyDamping();
        BounceOffScreenEdges();
    }

    private void ApplyStickyDamping()
    {
        float speed = Focus.Rigidbody.linearVelocity.magnitude;
        if (speed <= 0f || speed >= Focus.StickyThreshold) return;

        if (speed < Focus.SnapThreshold)
        {
            Focus.Rigidbody.linearVelocity = Vector2.zero;
            return;
        }

        float t = 1f - (speed / Focus.StickyThreshold);
        Focus.Rigidbody.linearVelocity *= Mathf.Max(0f, 1f - t * Focus.StickyDamping * Time.fixedDeltaTime);
    }

    private void BounceOffScreenEdges()
    {
        Camera cam = Camera.main;
        Vector2 pos = Focus.transform.position;
        Vector2 vel = Focus.Rigidbody.linearVelocity;
        Vector2 extents = Focus.Collider != null
            ? (Vector2)Focus.Collider.bounds.extents
            : Vector2.one * 0.5f;

        Vector2 min = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
        Vector2 max = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));

        bool hit = false;

        if (pos.x - extents.x < min.x && vel.x < 0f)      { vel.x = -vel.x; pos.x = min.x + extents.x; hit = true; }
        else if (pos.x + extents.x > max.x && vel.x > 0f) { vel.x = -vel.x; pos.x = max.x - extents.x; hit = true; }

        if (pos.y - extents.y < min.y && vel.y < 0f)      { vel.y = -vel.y; pos.y = min.y + extents.y; hit = true; }
        else if (pos.y + extents.y > max.y && vel.y > 0f) { vel.y = -vel.y; pos.y = max.y - extents.y; hit = true; }

        if (!hit) return;
        Focus.transform.position = pos;
        Focus.Rigidbody.linearVelocity = vel;
    }
}
