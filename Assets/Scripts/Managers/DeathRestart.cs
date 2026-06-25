using System.Collections;
using hoZer;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The player-death restart sequence. Armed by <see cref="St_Pl_OnDeath"/>; on the next scene load (the
/// retry) it freezes every StateMachine except the player's — the world holds still, "ready when you
/// are". As the player starts DRAGGING again the music winds back up (<see cref="MusicController.SpeedUp"/>);
/// the instant it starts FLYING, everything thaws and play resumes.
///
/// A plain MonoBehaviour (not a StateMachine), so the freeze it applies never touches itself, and it runs
/// its watch loop even while everything else is frozen.
/// </summary>
public class DeathRestart : MonoBehaviour
{
    static bool _armed;

    // Subscribe once at startup. Surviving editor Domain Reload being off means re-clearing any stale
    // subscription / flag from a previous play session first.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        _armed = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>Arm the sequence so the NEXT scene load runs the frozen "ready when you are" restart.</summary>
    public static void Arm() => _armed = true;

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_armed) return;
        _armed = false;

        new GameObject("[DeathRestart]").AddComponent<DeathRestart>();
    }

    IEnumerator Start()
    {
        // Let the freshly-loaded scene's StateMachines run their own Start() (which sets their initial
        // state) before we freeze them — Disable() only nulls the current state, and a Start running
        // afterwards would just re-establish it.
        yield return null;

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null) { Destroy(gameObject); yield break; }

        // Freeze everything but the player (CutsceneFreeze also pauses spawners — and never the GameManager,
        // which must stay live to orchestrate scene loads). The player drags and flies to bring it all back.
        CutsceneFreeze.FreezeAll(player);

        bool spedUp = false;

        while (player != null)
        {
            State s = player.Current;

            // Wind the music back up the moment they start a fresh pull.
            if (!spedUp && s is St_Pl_Dragging)
            {
                MusicController.Instance?.SpeedUp();
                spedUp = true;
            }

            // ...and the instant they launch, the world starts moving again.
            if (s is St_Pl_Flying)
            {
                if (!spedUp) MusicController.Instance?.SpeedUp(); // safety: caught the flight without a drag
                CutsceneFreeze.ThawAll();
                Destroy(gameObject);
                yield break;
            }

            yield return null;
        }

        // Player vanished (e.g. another reload) — never leave the world frozen.
        CutsceneFreeze.ThawAll();
        Destroy(gameObject);
    }
}
