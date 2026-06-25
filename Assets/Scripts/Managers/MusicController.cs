using System.Collections;
using UnityEngine;


/// <summary>
/// Persistent background music: plays an optional one-shot intro, then seamlessly loops the main
/// track forever. Survives scene loads (DontDestroyOnLoad) and is a singleton, so entering another
/// scene that also holds a MusicController won't restart or double up the song — the first one keeps
/// playing. Drop one into the first scene that should start the music (e.g. Tutorial 1); it carries
/// on through every later scene.
///
/// It creates its OWN dedicated AudioSources at runtime and never touches any other audio source.
/// The intro→loop hand-off is scheduled on the DSP clock (PlayScheduled), so it's sample-accurate
/// with no gap or click between the intro and the loop.
/// </summary>
public class MusicController : MonoBehaviour
{
    public static MusicController Instance { get; private set; }

    // Spawn the one persistent instance from Resources before the first scene loads — like the
    // GameManager — so it's present no matter which scene the game launches from (no need to place a
    // copy in every scene). The prefab lives at Assets/Resources/Music Controller.prefab.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;

        var prefab = Resources.Load<MusicController>("Music Controller");
        if (prefab == null)
        {
            Debug.LogWarning("[MusicController] No prefab at Resources/Music Controller — no background music.");
            return;
        }

        Instantiate(prefab);
    }

    [Header("Tracks")]
    [Tooltip("Optional one-shot played once before the loop. Leave empty to start looping immediately.")]
    public AudioClip intro;
    [Tooltip("The main track — looped forever once the intro finishes.")]
    public AudioClip loop;

    [Header("Playback")]
    [Range(0f, 1f)] public float volume = 0.6f;

    private AudioSource _introSource;
    private AudioSource _loopSource;
    private AudioLowPassFilter _lowPass;

    private void Awake()
    {
        // Singleton: the first instance wins and persists; any later scene copy destroys itself so
        // the music plays on uninterrupted instead of restarting from the top.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);   // be a root object so DontDestroyOnLoad reliably persists us
        DontDestroyOnLoad(gameObject);

        _introSource     = CreateSource();
        _loopSource      = CreateSource();
        _loopSource.loop = true;

        Play();
    }

    // A fresh, dedicated 2D source — separate from every other AudioSource in the game.
    private AudioSource CreateSource()
    {
        AudioSource s  = gameObject.AddComponent<AudioSource>();
        s.playOnAwake  = false;
        s.spatialBlend = 0f; // 2D: full volume regardless of position
        s.volume       = volume;
        return s;
    }

    private void Update()
    {
        // Keep both sources tracking the Inspector volume so it can be tuned live.
        if (_introSource != null) _introSource.volume = volume;
        if (_loopSource  != null) _loopSource.volume  = volume;
    }

    /// <summary>Starts the intro→loop sequence, scheduling the loop to begin exactly as the intro ends.</summary>
    public void Play()
    {
        StopAllCoroutines();
        ResetAudioFx();   // clear any lingering slow-down (pitch / muffle) so the new track plays clean
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        // Make both clips fully RESIDENT before scheduling. They're DecompressOnLoad and, in a build,
        // aren't decompressed yet when Awake runs — so scheduling immediately lets the decompress
        // overrun the start, the intro slips in late, and it plays on top of the loop (the song
        // doubles). LoadAudioData is asynchronous, so we must WAIT for it to finish, not just kick it
        // off. (In the editor the clips are already in memory, which is why it only shows in a build.)
        if (intro != null) intro.LoadAudioData();
        if (loop  != null) loop.LoadAudioData();
        while (Loading(intro) || Loading(loop)) yield return null;

        // Lead-in so both scheduled starts are armed before the DSP clock reaches them.
        double startTime = AudioSettings.dspTime + 0.2;

        if (intro != null)
        {
            double loopStart = startTime + (double)intro.samples / intro.frequency;

            _introSource.clip = intro;
            _introSource.PlayScheduled(startTime);
            // Hard-cap the intro's end exactly at the loop's start, so even if it ever begins late it
            // can never bleed past the hand-off and double up with the loop.
            _introSource.SetScheduledEndTime(loopStart);

            if (loop != null)
            {
                _loopSource.clip = loop;
                _loopSource.PlayScheduled(loopStart);
            }
        }
        else if (loop != null)
        {
            _loopSource.clip = loop;
            _loopSource.PlayScheduled(startTime);
        }
    }

    private static bool Loading(AudioClip clip) =>
        clip != null && clip.loadState == AudioDataLoadState.Loading;

    /// <summary>Stops both tracks — e.g. call from an outro/credits scene.</summary>
    public void StopMusic()
    {
        if (_introSource != null) _introSource.Stop();
        if (_loopSource  != null) _loopSource.Stop();
    }

    /// <summary>
    /// Winds the music down like a powering-off tape — drops the pitch toward a halt while a lowpass
    /// filter muffles it. Use instead of <see cref="StopMusic"/> when an abrupt cut would feel jarring
    /// (e.g. the Beeg Dwarf cutscene). A later Play()/PlayTrack() clears the effect.
    ///
    /// With <paramref name="stopWhenDone"/> false (e.g. on player death) it leaves the slowed track quietly
    /// playing rather than stopping, so a later <see cref="SpeedUp"/> can ramp it straight back up from
    /// where it stalled — no re-scheduling, no gap.
    /// </summary>
    public void SlowToStop(float duration = 1.5f, bool stopWhenDone = true)
    {
        StopAllCoroutines();
        StartCoroutine(SlowToStopRoutine(Mathf.Max(0.01f, duration), stopWhenDone));
    }

    private IEnumerator SlowToStopRoutine(float duration, bool stopWhenDone)
    {
        AudioLowPassFilter lp = LowPass();

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // wind down even if the scene is paused / time-scaled
            float k = Mathf.Clamp01(t / duration);

            float pitch = Mathf.Lerp(1f, 0.01f, k);            // slow the playback toward a halt
            if (_introSource != null) _introSource.pitch = pitch;
            if (_loopSource  != null) _loopSource.pitch  = pitch;

            lp.cutoffFrequency = Mathf.Lerp(22000f, 300f, k);  // muffle it as it winds down

            yield return null;
        }

        // Stop dead and clear the effect — unless we're meant to stay quietly playing at the slowed pitch
        // so a later SpeedUp can wind us straight back up.
        if (stopWhenDone)
        {
            StopMusic();
            ResetAudioFx();
        }
    }

    /// <summary>
    /// Winds the music back UP like a tape spinning to speed — ramps the pitch from wherever it stalled
    /// back to normal while the lowpass re-opens. Pairs with <see cref="SlowToStop"/>(stopWhenDone: false).
    /// </summary>
    public void SpeedUp(float duration = 1.5f)
    {
        StopAllCoroutines();
        StartCoroutine(SpeedUpRoutine(Mathf.Max(0.01f, duration)));
    }

    private IEnumerator SpeedUpRoutine(float duration)
    {
        AudioLowPassFilter lp = LowPass();

        float startPitch  = _loopSource != null ? _loopSource.pitch : 0.01f;
        float startCutoff = lp.cutoffFrequency;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);

            float pitch = Mathf.Lerp(startPitch, 1f, k);
            if (_introSource != null) _introSource.pitch = pitch;
            if (_loopSource  != null) _loopSource.pitch  = pitch;

            lp.cutoffFrequency = Mathf.Lerp(startCutoff, 22000f, k);

            yield return null;
        }

        ResetAudioFx();
    }

    // The lowpass used by the slow-down, created on demand. Sits fully open (≈no effect) the rest of
    // the time, so leaving it on the object is harmless.
    private AudioLowPassFilter LowPass()
    {
        if (_lowPass == null)
        {
            _lowPass = GetComponent<AudioLowPassFilter>();
            if (_lowPass == null) _lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            _lowPass.cutoffFrequency = 22000f; // a fresh filter defaults to 5000Hz — open it so it's inert
        }
        return _lowPass;
    }

    // Clears any lingering slow-down: pitch back to normal and the lowpass fully open.
    private void ResetAudioFx()
    {
        if (_introSource != null) _introSource.pitch = 1f;
        if (_loopSource  != null) _loopSource.pitch  = 1f;
        if (_lowPass     != null) _lowPass.cutoffFrequency = 22000f;
    }

    /// <summary>
    /// Switches to a new track and starts it (optional one-shot <paramref name="newIntro"/>, then loops
    /// <paramref name="newLoop"/> forever) — e.g. the Beeg Dwarf cutscene bringing up its battle theme as
    /// it ends. Stops whatever was playing first so they can't overlap.
    /// </summary>
    public void PlayTrack(AudioClip newLoop, AudioClip newIntro = null)
    {
        StopMusic();
        intro = newIntro;
        loop  = newLoop;
        Play();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
