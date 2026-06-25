using System.Collections.Generic;
using UnityEngine;

namespace hoZer
{
    /// <summary>
    /// Freezes and thaws the scene's StateMachines for a cutscene. <see cref="FreezeAll"/> disables every
    /// machine (so the gameplay pump goes idle) except the ones passed in; <see cref="ThawAll"/> re-enables
    /// exactly the ones it froze, restoring each to the state it was mid-running. It also disables any
    /// <see cref="EnemySpawner"/> (a plain MonoBehaviour the pump-freeze wouldn't otherwise stop) so no
    /// new enemies arrive mid-cutscene, and pauses the <see cref="TimeUI"/> level timer — all re-enabled
    /// on thaw. One cutscene at a time — the frozen set is static, and FreezeAll clears any prior freeze
    /// first so they can't stack.
    /// </summary>
    public static class CutsceneFreeze
    {
        static readonly List<StateMachine> _frozen   = new();
        static readonly List<Behaviour>    _disabled = new(); // non-StateMachine drivers, e.g. EnemySpawner

        /// <summary>Disable every StateMachine in the scene except <paramref name="except"/>, plus all spawners.</summary>
        public static void FreezeAll(params StateMachine[] except)
        {
            ThawAll(); // never stack freezes

            foreach (StateMachine m in Object.FindObjectsByType<StateMachine>(FindObjectsSortMode.None))
            {
                if (m == null) continue;
                if (except != null && System.Array.IndexOf(except, m) >= 0) continue;

                m.Disable();
                _frozen.Add(m);
            }

            // Spawners aren't StateMachines, so freezing the pump leaves them spawning — disable them
            // outright so the cutscene isn't interrupted by fresh enemies.
            foreach (EnemySpawner s in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
            {
                if (s == null || !s.enabled) continue;

                s.enabled = false;
                _disabled.Add(s);
            }

            // Pause the level timer too (it counts in its own Update) so frozen time isn't charged to the
            // player. ThawAll re-enables it; the death-restart re-enables it earlier, on the first input.
            if (TimeUI.Instance != null && TimeUI.Instance.enabled)
            {
                TimeUI.Instance.enabled = false;
                _disabled.Add(TimeUI.Instance);
            }
        }

        /// <summary>
        /// Re-enable everything FreezeAll disabled — StateMachines (restored to their prior state) and the
        /// spawners — except any StateMachines passed in <paramref name="except"/>, which are left as-is
        /// (e.g. one the caller is about to drive into a fresh state itself). Everything is cleared either way.
        /// </summary>
        public static void ThawAll(params StateMachine[] except)
        {
            foreach (StateMachine m in _frozen)
                if (m != null && (except == null || System.Array.IndexOf(except, m) < 0))
                    m.Enable();

            _frozen.Clear();

            foreach (Behaviour b in _disabled)
                if (b != null) b.enabled = true;

            _disabled.Clear();
        }
    };
};
