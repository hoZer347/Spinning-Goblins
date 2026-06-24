using UnityEngine;

namespace hoZer.Dialogue
{
    /// <summary>
    /// The Beeg Dwarf intro cutscene's dialogue manager. Adds two bindings on top of the inherited
    /// defaults (Pause / TextSpeed / Clear / Proceed):
    ///   [StartSpin] — the dwarf begins its in-place wind-up spin and speeds up while it talks.
    ///   [Done]      — ends the cutscene: thaws every frozen StateMachine, launches the dwarf into its
    ///                 real spin attack (so the whirl carries straight into gameplay), and destroys this
    ///                 manager's GameObject (this subclass only — the base DialogueManager is untouched).
    /// Wire it onto the cutscene's dialogue object exactly like a <see cref="DialogueManager"/> (script,
    /// dialogue box, continue text, font); <see cref="FirstTimeCutsceneTrigger"/> sets <see cref="Dwarf"/>
    /// and calls <see cref="DialogueManager.Begin"/> when the dwarf lands.
    /// </summary>
    public class BeegDwarfDialogueManager : DialogueManager
    {
        /// <summary>The dwarf this cutscene controls. Set by the trigger right before the dialogue begins.</summary>
        [HideInInspector] public EnemyController Dwarf;

        /// <summary>Song to bring up as the cutscene ends, via the MusicController (optional intro + loop). Set by the trigger.</summary>
        [HideInInspector] public AudioClip EndMusicIntro;
        [HideInInspector] public AudioClip EndMusicLoop;

        /// <summary>The player, held in St_Pl_CutsceneHold during the dialogue; returned to Idle at the end. Set by the trigger.</summary>
        [HideInInspector] public PlayerController Player;

        /// <summary>The dwarf starts whirling in place, winding up faster and faster.</summary>
        [DialogueBinding] void StartSpin()
        {
            if (Dwarf != null) Dwarf.SetState<St_En1_CutsceneSpin>();
        }

        /// <summary>
        /// Like Proceed, but ends the whole cutscene: re-enable every machine we froze, carry the dwarf's
        /// spin into a real spin attack, and hide the persistent dialogue object again (NOT destroy it —
        /// it lives on DontDestroyOnLoad, ready for later scenes).
        /// </summary>
        [DialogueBinding] void Done()
        {
            // Carry the spin straight into gameplay — the dwarf releases its real spin attack. Do this
            // FIRST, while it's still in St_En1_CutsceneSpin, so that state's OnExit runs and restores
            // the body scale before SpinAttack captures it.
            if (Dwarf != null) Dwarf.SetState<St_En1_SpinAttack>();

            // Reenable every gameplay StateMachine the cutscene froze — but NOT the dwarf, which we just
            // set ourselves (Enable would otherwise clobber it back to its pre-freeze state).
            CutsceneFreeze.ThawAll(Dwarf);

            // Hand control back to the player: out of the cutscene hold and into Idle.
            if (Player != null) Player.SetState<St_Pl_Idle>();

            // Bring up the new song as the cutscene ends (the music was cut when the dwarf dropped in).
            if (EndMusicLoop != null && MusicController.Instance != null)
                MusicController.Instance.PlayTrack(EndMusicLoop, EndMusicIntro);

            // Drop the queued end-of-dialogue state and go dormant so the binding's automatic Proceed
            // can't drive the manager any further this frame.
            Clear();
            Disable();

            // Put the persistent dialogue object away again instead of destroying it, so it stays
            // available (hidden) for the rest of this session. Falls back to disabling our own root if
            // this manager is a one-off placed directly in a scene rather than the persistent prefab.
            if (PersistentDialogue.Instance != null) PersistentDialogue.Instance.Hide();
            else gameObject.SetActive(false);
        }
    };
};
