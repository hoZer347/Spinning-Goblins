using Tymski;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace hoZer.Dialogue
{
    /// <summary>
    /// Story dialogue for the intro cutscene. It is a plain <see cref="DialogueManager"/> — the exact same
    /// dialogue-box UI, streaming text, continue prompt and Banjo-Kazooie voice — with nothing from the
    /// Beeg Dwarf cutscene (no dwarf, no world-freeze, no player, no spin). It just adds:
    ///   • auto-play on scene start (so the cutscene narrates itself), and
    ///   • a [Done] binding that ends the dialogue and moves on to the next scene.
    ///
    /// Wire it onto the cutscene's dialogue object exactly like a <see cref="DialogueManager"/> (script,
    /// dialogue box, continue text, font, voice), write the story in the TextAsset, and end that script
    /// with [Done]. Leave the StateMachine's Start State empty — auto-play drives it through Begin().
    /// </summary>
    public class IntroCinematicDialogueManager : DialogueManager
    {
        [Header("Cutscene")]
        [Tooltip("Start streaming automatically when the scene loads. Turn off to drive it yourself by " +
                 "calling Begin() from elsewhere (a trigger, a panel, etc.).")]
        [SerializeField] bool playOnStart = true;

        [Tooltip("Scene loaded when the dialogue ends (via [Done]). Routed through the GameManager for the " +
                 "normal swipe transition; direct-loads if there's no GameManager. Leave empty to use the " +
                 "default post-cutscene route (GameManager.OnCutsceneComplete).")]
        [SerializeField] SceneReference nextScene;

        [Tooltip("The CutsceneManager driving the panel slideshow. Required for [NextPanel] to work.")]
        [SerializeField] CutsceneManager cutsceneManager;

        // The dialogue managers don't use a Start State — they're kicked off by Begin() (which queues the
        // script fresh and proceeds). Do that here on start so the cutscene plays on its own. base.OnStart
        // builds + validates first; Begin() no-ops the build and just streams.
        protected override void OnStart()
        {
            // Auto-find CutsceneManager if not wired in the Inspector.
            cutsceneManager ??= FindObjectOfType<CutsceneManager>();

            // If a CutscenePanel owns this dialogue, it calls Begin() via Show() — don't auto-play.
            if (GetComponent<CutscenePanel>() != null)
                playOnStart = false;

            base.OnStart();
            if (playOnStart) Begin();
        }

        /// <summary>
        /// Ends the cutscene dialogue and moves on. Clear() empties the queued end state and Disable() stops
        /// the pump, so the InvokeBinding state's trailing Proceed (it fires right after this binding) can't
        /// re-run the dialogue — then load the next scene. (Bound by NAME, so the script's [Done] calls this.)
        /// </summary>
        [DialogueBinding] void NextPanel()
        {
            if (cutsceneManager != null) cutsceneManager.AdvancePanel();
            else Debug.LogWarning("[IntroCinematic] [NextPanel] called but no CutsceneManager assigned.");
        }

        [DialogueBinding] void Done()
        {
            Clear();
            Disable();
            LoadNextScene();
        }

        // Hand off the same way the panel cutscene does (St_Cs_Complete): through the GameManager for the
        // swipe transition, with a direct load as a no-GameManager fallback.
        void LoadNextScene()
        {
            GameManager gm        = GameManager.Instance;
            bool        haveScene = nextScene != null && !string.IsNullOrEmpty(nextScene.ScenePath);

            if (gm != null && haveScene) gm.LoadScene(nextScene);
            else if (haveScene)          SceneManager.LoadScene(nextScene.ScenePath);
            else if (gm != null)         gm.OnCutsceneComplete();
            else Debug.LogWarning("[IntroCinematic] No next scene set and no GameManager — nothing to load.");
        }
    }
}
