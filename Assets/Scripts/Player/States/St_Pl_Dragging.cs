using hoZer;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


[Serializable]
public class St_Pl_Dragging : St_Pl_Base
{
    private const int MaxLineBounces = 1;

    private Vector2 _origin;
    Duration _wooshSFXDelay;

    public override void OnEnter(State lastState)
    {
        Focus.Rigidbody.bodyType = RigidbodyType2D.Kinematic;
        Focus.Rigidbody.linearVelocity = Vector2.zero;
        _origin = Focus.transform.position;

        if (Focus.DragLine != null)
        {
            Focus.DragLine.enabled = true;
            Focus.DragLine.positionCount = 0;
            Focus.DragLine.startWidth = 0.04f;
            Focus.DragLine.endWidth = 0.01f;
        }
        ;

		Focus.audioSource.PlayOneShot(Focus.stretch, 0.5f);

		Focus.ResetSpin();
	}

    public override void OnUpdate()
    {
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 dragVec = mouseWorld - Focus.DragClickPosition;

        if (dragVec.magnitude > Focus.MaxDragDistance)
            dragVec = dragVec.normalized * Focus.MaxDragDistance;

        // Launch power comes from the FULL pull, so compression and the wall / screen clamping
        // below never weaken the shot — same output velocity however far the body stretches.
        Focus.LaunchForce = -dragVec * Focus.LaunchForceMultiplier;

        // Spin rate ramps with the forward push it'll get (the launch power), not the stretch.
        Focus.SpinTick(Focus.LaunchForce.magnitude);

        // Compress the visible stretch, then stop it short of any wall it would poke into.
        Vector2 offset = ClampOutOfWalls(_origin, dragVec * Focus.StretchCompression);

        // Keep the body on screen.
        Vector2 targetPos = _origin + offset;
        Vector2 extents = Focus.Collider != null ? (Vector2)Focus.Collider.bounds.extents : Vector2.one * 0.5f;
        Vector2 sMin = Camera.main.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
        Vector2 sMax = Camera.main.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
        targetPos.x = Mathf.Clamp(targetPos.x, sMin.x + extents.x, sMax.x - extents.x);
        targetPos.y = Mathf.Clamp(targetPos.y, sMin.y + extents.y, sMax.y - extents.y);

        Focus.transform.position = new Vector3(targetPos.x, targetPos.y, Focus.transform.position.z);

        // Line from the pulled-back body, through origin, out along the launch direction.
        if (Focus.DragLine != null)
        {
            Focus.DragLine.positionCount = 2;
            Focus.DragLine.SetPosition(0, Focus.transform.position);
            Focus.DragLine.SetPosition(1, (Vector3)(_origin - dragVec) + new Vector3(0, 0, Focus.transform.position.z));
        }
        ;

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            SetState<St_Pl_Flying>();
    }

    // Stops the stretch offset short of any obstacle it would enter, keeping its direction so the
    // launch aim (and resulting velocity) is unchanged.
    private Vector2 ClampOutOfWalls(Vector2 origin, Vector2 offset)
    {
        if (Focus.Collider == null || offset == Vector2.zero)
            return offset;

        float radius = Focus.Collider.bounds.extents.x;
        RaycastHit2D hit = Physics2D.CircleCast(
            origin, radius, offset.normalized, offset.magnitude, Focus.ObstacleLayer);

        return hit.collider != null
            ? offset.normalized * hit.distance
            : offset;
    }

    public override void OnExit(State nextState)
    {
        // Launch from where the body actually is (the stretched position) instead of snapping
        // back to the drag origin — the flight begins from where the player starts moving.
        if (Focus.DragLine != null) Focus.DragLine.enabled = false;

        Focus.audioSource.PlayOneShot(Focus.spinWoosh);
    }
}
