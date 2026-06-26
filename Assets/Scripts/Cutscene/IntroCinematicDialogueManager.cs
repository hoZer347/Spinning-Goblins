using System.Collections;
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

        [Header("Music")]
        [Tooltip("Optional one-shot played once before the loop. Leave empty for a single looping track.")]
        [SerializeField] AudioClip musicIntro;
        [Tooltip("The cutscene's song, brought up (and looped) by the [PlayMusic] binding from the dialogue " +
                 "script. Leave both clips empty to play nothing.")]
        [SerializeField] AudioClip musicLoop;

        [Tooltip("Seconds for the [StifleMusic] wind-down and the [UnstifleMusic] wind-up ramps.")]
        [SerializeField] float stifleDuration = 1f;

        [Header("End transition")]
        [Tooltip("When the cutscene ends ([Done]), seconds spent fading the screen to black AND the music " +
                 "out together before loading the next scene. 0 = no fade, load immediately.")]
        [SerializeField] float endFadeDuration = 2f;

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

        /// <summary>
        /// Brings up the cutscene's song (optional intro, then loop) through the persistent MusicController —
        /// driven from the dialogue script via [PlayMusic], so the music starts exactly where the writer wants
        /// rather than at scene load. Mirrors the Beeg Dwarf cutscene's [PlayMusic]; routes through PlayTrack,
        /// the no-overlap path, so the intro→loop hand-off is safe on the WebGL build.
        /// </summary>
        [DialogueBinding] void PlayMusic()
        {
            if (musicLoop != null && MusicController.Instance != null)
                MusicController.Instance.PlayTrack(musicLoop, musicIntro);
        }

        /// <summary>
        /// [StifleMusic] — winds the music down (slows the pitch + muffles + fades, the same effect as just
        /// before the Beeg Dwarf cutscene) but leaves it quietly playing, so [UnstifleMusic] can ramp it
        /// straight back up. Reuses the shared MusicController.SlowToStop, so nothing else changes.
        /// </summary>
        [DialogueBinding] void StifleMusic()
        {
            if (MusicController.Instance != null)
                MusicController.Instance.SlowToStop(stifleDuration, stopWhenDone: false);
        }

        /// <summary>[UnstifleMusic] — winds the music back up to full speed/volume after [StifleMusic].</summary>
        [DialogueBinding] void UnstifleMusic()
        {
            if (MusicController.Instance != null)
                MusicController.Instance.SpeedUp(stifleDuration);
        }

        [DialogueBinding] void Done()
        {
            // Clear()+Disable() FIRST (synchronously) so the InvokeBinding state's trailing Proceed can't
            // re-run the dialogue while we fade out.
            Clear();
            Disable();

            if (endFadeDuration > 0f) StartCoroutine(FadeOutThenLoad());
            else                      LoadNextScene();
        }

        // Slowly fade the screen to black and the music out together, then hand off to the next scene. The
        // music fade is a clean stop (web-safe) so the next song — Tutorial 1's, started on the player's
        // first click — comes up fresh with no overlap.
        IEnumerator FadeOutThenLoad()
        {
            if (MusicController.Instance != null)
                MusicController.Instance.FadeOutAndStop(endFadeDuration);

            ScreenFader fader = FindObjectOfType<ScreenFader>();
            if (fader == null)
                fader = new GameObject("ScreenFader").AddComponent<ScreenFader>();

            float t = 0f;
            while (t < endFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fader.Alpha = Mathf.Clamp01(t / endFadeDuration);
                yield return null;
            }
            fader.Alpha = 1f;

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
