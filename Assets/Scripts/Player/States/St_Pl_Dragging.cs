using hoZer;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


[Serializable]
public class St_Pl_Dragging : St_Pl_Base
{
    private const int MaxLineBounces = 1;

    // The goblin must be pulled at least this far off its rest spot before enemies can hurt it during
    // a drag. Below this the body is sitting on (or barely off) the origin — the first frames of a
    // pull, or a pull a wall won't let happen — and an enemy touching it there would score a hit "at
    // the origin". Past it the body is genuinely pulled back and the enemy must reach wherever it sits.
    private const float DragHurtMinPull = 0.5f;

    private Vector2 _origin;
    private Vector2 _virtualScreen; // unclamped virtual cursor; the screen edge can't cap the pull
    Duration _wooshSFXDelay;

    public override void OnEnter(State lastState)
    {
        Focus.Rigidbody.bodyType = RigidbodyType2D.Kinematic;
        Focus.Rigidbody.linearVelocity = Vector2.zero;

        // Continuous collision detection SWEEPS the collider along the path from its previous physics
        // position to the new one and reports anything on that line as a hit. While dragging we hand-
        // position the body every frame, so that path runs from the drag origin out to the stretched
        // spot — and an enemy anywhere along it (including back at the origin) lands a hit, even though
        // the goblin is pulled away. Discrete detection collides only at the collider's ACTUAL current
        // position. Continuous is only needed for the fast flight (anti-tunneling); restored on exit.
        Focus.Rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

        _origin        = Focus.transform.position;

        // Lock (and hide) the OS cursor for the pull. Raw mouse movement then drives it and keeps
        // flowing even when the pointer would run off-screen, so the pull charges to FULL strength while
        // the goblin — and the on-screen cursor sprite the CursorManager draws on it — stay clamped at
        // the screen edge. Start the virtual cursor at the click so the pull begins at zero. Released in OnExit.
        Cursor.lockState = CursorLockMode.Locked;
        _virtualScreen   = Camera.main.WorldToScreenPoint(Focus.DragClickPosition);

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
        // The cursor is locked, so raw mouse movement drives a VIRTUAL position the screen edge can't
        // cap — the delta keeps accumulating even as the pointer would run off-screen, so the pull
        // charges to full strength while the goblin stays clamped on-screen.
        _virtualScreen += Mouse.current.delta.ReadValue();

        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(_virtualScreen);
        Vector2 dragVec = mouseWorld - Focus.DragClickPosition;

        if (dragVec.magnitude > Focus.MaxDragDistance)
        {
            dragVec = dragVec.normalized * Focus.MaxDragDistance;
            _virtualScreen = Camera.main.WorldToScreenPoint(Focus.DragClickPosition + dragVec);
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

        // Move the WHOLE body to the pulled-back spot — root, Rigidbody, collider and all. The earlier
        // approach moved only the Sprite child and parked the Rigidbody/collider at the drag origin,
        // which is exactly why enemies could only connect at the origin and why the body kept snapping
        // back there. A hand-moved Kinematic body DOES snap back if you set only transform.position: the
        // physics writeback overwrites it from Rigidbody.position every step. Setting Rigidbody.position
        // TOO pins the body at the pulled-back spot, so the collider, the sprite (riding the root at
        // local 0), enemy targeting (PlayerController.Position) and the hurt check are all there — and
        // there is no origin-parked body left to jump back to. Detection is Discrete during the drag, so
        // moving the body each frame never sweeps a hit along the path from the origin.
        Focus.transform.position = new Vector3(targetPos.x, targetPos.y, Focus.transform.position.z);
        Focus.Rigidbody.position = targetPos;

        // Hurt is evaluated by an explicit overlap at the goblin's body, every frame — more reliable
        // than waiting on a physics contact event while the body is Kinematic. Only once the body has
        // actually been pulled clear of the rest spot, though: while it's still sitting on the origin
        // (drag just started, or a wall is blocking the pull) an enemy reaching it must NOT score the
        // hit "at the origin" — the goblin only looks pulled back there because the sprite elongates.
        if ((targetPos - _origin).sqrMagnitude > DragHurtMinPull * DragHurtMinPull && CheckDragHurt(targetPos))
            return;

        // Line from the pulled-back body (the sprite, now the stretched position), through origin,
        // out along the launch direction.
        Vector3 lineStart = new Vector3(targetPos.x, targetPos.y, Focus.transform.position.z);
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

    // Hurts the player when a live enemy's body overlaps the goblin's at the pulled-back spot. The
    // whole body (collider included) is moved there during the drag, so a plain collider overlap
    // means the enemy has genuinely reached the goblin — no origin/sprite split to correct for, and
    // so no extra distance gate. Returns true once a hit is taken (the state has already changed).
    private bool CheckDragHurt(Vector2 bodyCenter)
    {
        float radius = Focus.Collider != null ? Focus.Collider.bounds.extents.x : 0.5f;

        foreach (Collider2D c in Physics2D.OverlapCircleAll(bodyCenter, radius))
        {
            EnemyController enemy = c.GetComponentInParent<EnemyController>();
            if (enemy == null) continue;

            // A reeling, dying or falling enemy is harmless — only a live one threatens the player.
            bool harmless = enemy.Current is St_En1_Hitstun
                || enemy.Current is St_En1_Death
                || enemy.Current is St_En1_Falling;
            if (harmless) continue;

            // [HURT DEBUG] temporary — where is the goblin body, the enemy, and the rest spot at the
            // instant damage lands? bodyToOrigin says whether we were actually pulled back or not.
            Debug.Log($"[HURT] goblinBody={bodyCenter} enemy={(Vector2)enemy.transform.position} "
                + $"restOrigin={_origin} bodyToOrigin={Vector2.Distance(bodyCenter, _origin):0.00} "
                + $"enemyToBody={Vector2.Distance((Vector2)enemy.transform.position, bodyCenter):0.00} "
                + $"enemyToOrigin={Vector2.Distance((Vector2)enemy.transform.position, _origin):0.00}");
            HurtFromEnemy(enemy);
            return true;
        }
        return false;
    }

    // Stops the stretch offset short of any obstacle it would enter, keeping its direction so the
    // launch aim (and resulting velocity) is unchanged.
    private Vector2 ClampOutOfWalls(Vector2 origin, Vector2 offset)
    {
        if (Focus.Collider == null || offset == Vector2.zero)
            return offset;

        float radius = Focus.Collider.bounds.extents.x;

        // Enemies sit on the Obstacle layer too (so the flying goblin bounces off them), but an enemy
        // is NOT a wall — clamping the pull-back against one lets an approaching enemy shove the aim
        // (and the body) back toward the origin, then snap it home when the enemy reaches the rest spot.
        // Cast for ALL obstacle hits and take the nearest that ISN'T an enemy, so only real walls clamp.
        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            origin, radius, offset.normalized, offset.magnitude, Focus.ObstacleLayer);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.GetComponentInParent<EnemyController>() != null) continue; // enemies aren't walls
            return offset.normalized * hit.distance; // nearest real wall (CircleCastAll is distance-sorted)
        }

        return offset; // nothing but enemies (or empty) in the way — full stretch
    }

    // Suppress physics-based enemy contacts while the root collider sits at the drag origin.
    // The only enemy damage path during a drag is CheckDragHurt — an explicit per-frame circle
    // at the sprite's drawn position. This ensures enemies must reach the pulled-back sprite,
    // not just touch the stationary root, to deal damage.
    public override void OnContact(Collider2D other)
    {
        EnemyController enemy = other.GetComponent<EnemyController>()
            ?? other.GetComponentInParent<EnemyController>();
        if (enemy != null) return;

        base.OnContact(other);
    }

    public override void OnExit(State nextState)
    {
        // Release the cursor — it's free to roam (and leave the window) again outside the drag.
        Cursor.lockState = CursorLockMode.None;

        // Restore continuous detection for the launch — the flight is fast enough to tunnel walls.
        Focus.Rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Hand the sprite's stretched position back to the body so the launch (and the collider, and
        // the knockback if we were hit) begins from where the goblin is drawn — then re-centre the
        // sprite on the body so every non-drag state has the visual back on the root again.
        if (Focus.Sprite != null)
        {
            Vector3 launchPos = Focus.Sprite.transform.position;
            Focus.transform.position = new Vector3(launchPos.x, launchPos.y, Focus.transform.position.z);
            Focus.Rigidbody.position = launchPos;
            Focus.Sprite.transform.localPosition = Vector3.zero;

            // Root is now at the sprite position — collider offset back to zero so it stays centred.
            if (Focus.Collider != null)
                Focus.Collider.offset = Vector2.zero;

            // The root's Kinematic Rigidbody sits at the drag origin the whole pull. Without a
            // forced sync here, the Kinematic writeback in the next FixedUpdate would read the
            // physics body's OLD simulated position (the drag origin) and overwrite transform.position
            // back to (0,0) before the Dynamic transition takes effect — causing a one-frame snap.
            // SyncTransforms pushes the new transform position into the physics simulation NOW so
            // the writeback reads launchPos instead.
            Physics2D.SyncTransforms();
        }

        // Launch from where the body actually is (the stretched position) instead of snapping
        // back to the drag origin — the flight begins from where the player starts moving.
        if (Focus.DragLine != null)       Focus.DragLine.enabled       = false;
        if (Focus.DragLineShadow != null) Focus.DragLineShadow.enabled = false;

        Focus.audioSource.PlayOneShot(Focus.spinWoosh);
    }
}
