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

        string spriteLayer = Focus.Sprite != null ? Focus.Sprite.sortingLayerName : "Default";
        int    spriteOrder = Focus.Sprite != null ? Focus.Sprite.sortingOrder     : 0;

        if (Focus.DragLine != null)
        {
            Focus.DragLine.enabled            = true;
            Focus.DragLine.positionCount      = 0;
            Focus.DragLine.startWidth         = 0.09f;
            Focus.DragLine.endWidth           = 0.03f;
            Focus.DragLine.startColor         = Color.white;
            Focus.DragLine.endColor           = Color.white;
            Focus.DragLine.material.color     = Color.white;
            Focus.DragLine.sortingLayerName   = spriteLayer;
            Focus.DragLine.sortingOrder       = spriteOrder - 1;
        }

        if (Focus.DragLineShadow != null)
        {
            Focus.DragLineShadow.enabled            = true;
            Focus.DragLineShadow.positionCount      = 0;
            Focus.DragLineShadow.startWidth         = 0.15f;
            Focus.DragLineShadow.endWidth           = 0.06f;
            Focus.DragLineShadow.startColor         = Color.black;
            Focus.DragLineShadow.endColor           = Color.black;
            Focus.DragLineShadow.material.color     = Color.black;
            Focus.DragLineShadow.sortingLayerName   = spriteLayer;
            Focus.DragLineShadow.sortingOrder       = spriteOrder - 2;
        }

		Focus.audioSource.PlayOneShot(Focus.stretch, 0.5f);

		Focus.ResetSpin();
	}

    public override void OnUpdate()
    {
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 dragVec = mouseWorld - Focus.DragClickPosition;

        if (dragVec.magnitude > Focus.MaxDragDistance)
        {
            dragVec = dragVec.normalized * Focus.MaxDragDistance;
            Vector2 clampedScreen = Camera.main.WorldToScreenPoint(Focus.DragClickPosition + dragVec);
            Mouse.current.WarpCursorPosition(clampedScreen);
        }

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
        Vector3 lineStart = Focus.transform.position;
        Vector3 lineEnd   = (Vector3)(_origin - dragVec) + new Vector3(0, 0, Focus.transform.position.z);

        if (Focus.DragLine != null)
        {
            Focus.DragLine.positionCount = 2;
            Focus.DragLine.SetPosition(0, lineStart);
            Focus.DragLine.SetPosition(1, lineEnd);
        }

        if (Focus.DragLineShadow != null)
        {
            Focus.DragLineShadow.positionCount = 2;
            Focus.DragLineShadow.SetPosition(0, lineStart);
            Focus.DragLineShadow.SetPosition(1, lineEnd);
        }

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
        if (Focus.DragLine != null)       Focus.DragLine.enabled       = false;
        if (Focus.DragLineShadow != null) Focus.DragLineShadow.enabled = false;

        Focus.audioSource.PlayOneShot(Focus.spinWoosh);
    }
}
