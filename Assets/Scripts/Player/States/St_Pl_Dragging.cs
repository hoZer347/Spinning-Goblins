using hoZer;
using System;
using UnityEngine;
using UnityEngine.InputSystem;


[Serializable]
public class St_Pl_Dragging : St_Pl_Base
{
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
            Focus.DragLine.positionCount = 2;
            Focus.DragLine.startWidth = 0.04f;
            Focus.DragLine.endWidth = 0.01f;
        };

		Focus.audioSource.PlayOneShot(Focus.stretch);
	}

    public override void OnUpdate()
    {
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 dragVec = mouseWorld - Focus.DragClickPosition;

        if (dragVec.magnitude > Focus.MaxDragDistance)
            dragVec = dragVec.normalized * Focus.MaxDragDistance;

        // Clamp so player can't be dragged off screen
        Vector2 targetPos = _origin + dragVec;
        Vector2 extents = Focus.Collider != null ? (Vector2)Focus.Collider.bounds.extents : Vector2.one * 0.5f;
        Vector2 sMin = Camera.main.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
        Vector2 sMax = Camera.main.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
        targetPos.x = Mathf.Clamp(targetPos.x, sMin.x + extents.x, sMax.x - extents.x);
        targetPos.y = Mathf.Clamp(targetPos.y, sMin.y + extents.y, sMax.y - extents.y);
        dragVec = targetPos - _origin;

        Focus.transform.position = new Vector3(targetPos.x, targetPos.y, Focus.transform.position.z);
        Focus.LaunchForce = -dragVec * Focus.LaunchForceMultiplier;

        // Line from pulled-back player through origin and out the same distance in launch direction
        if (Focus.DragLine != null)
        {
            Focus.DragLine.SetPosition(0, Focus.transform.position);
            Focus.DragLine.SetPosition(1, (Vector3)(_origin - dragVec) + new Vector3(0, 0, Focus.transform.position.z));
        }

		//if (_wooshSFXDelay.Tick())
		//{
		//	Focus.audioSource.PlayOneShot(Focus.spinWoosh);
		//	_wooshSFXDelay.Reset(0.5f);
		//};

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            SetState<St_Pl_Flying>();
    }

    public override void OnExit(State nextState)
    {
        Focus.transform.position = (Vector3)_origin + new Vector3(0, 0, Focus.transform.position.z);
        if (Focus.DragLine != null) Focus.DragLine.enabled = false;

		Focus.audioSource.PlayOneShot(Focus.spinWoosh);
	}
}
