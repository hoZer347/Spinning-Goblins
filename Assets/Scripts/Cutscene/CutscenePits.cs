using System.Collections.Generic;
using UnityEngine;

namespace hoZer
{
    /// <summary>
    /// Temporarily switches off the Pit colliders for a cutscene. While the world is frozen and the dwarf
    /// is teleported above the screen and dropped, a pit must not be able to claim anything: the swept
    /// pit-crossing test on the teleport could otherwise draw a line across an edge pit and drop the boss
    /// into it — destroying the dwarf, killing the cutscene coroutine, and leaving a blank, frozen mess.
    ///
    /// <see cref="Enable"/> restores exactly what <see cref="Disable"/> turned off, and is idempotent and
    /// safe to over-call (the cutscene exit, the dialogue's OnDisable/OnDestroy, etc. all call it).
    /// </summary>
    public static class CutscenePits
    {
        static readonly List<Collider2D> _disabled = new();

        /// <summary>Disable every enabled collider on the "Pits" layer, remembering them for <see cref="Enable"/>.</summary>
        public static void Disable()
        {
            Enable(); // never stack — restore any prior set first

            int pits = LayerMask.NameToLayer("Pits");
            if (pits < 0) return; // no such layer in this project

            foreach (Collider2D c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                if (c != null && c.enabled && c.gameObject.layer == pits)
                {
                    c.enabled = false;
                    _disabled.Add(c);
                }
        }

        /// <summary>Re-enable the pit colliders disabled by <see cref="Disable"/>. No-op if none are off.</summary>
        public static void Enable()
        {
            foreach (Collider2D c in _disabled)
                if (c != null) c.enabled = true; // skips any destroyed by a scene unload mid-cutscene

            _disabled.Clear();
        }
    }
}
