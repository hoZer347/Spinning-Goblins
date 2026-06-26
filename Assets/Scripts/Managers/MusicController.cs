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
/// The main track repeats using the AudioSource's own loop flag (a core feature that works on every
/// platform, including WebGL / itch.io); the optional intro plays once first and only hands off to the
/// loop after it has fully finished, so the two can never overlap.
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

    // The intro→loop play sequence and any active fade run as SEPARATE coroutines, tracked here. A fade
    // (slow-down / speed-up) must NOT abort the intro→loop hand-off — doing so via StopAllCoroutines was
    // the bug where a fade firing mid-intro (e.g. a death right after a new track's intro began) left the
    // loop never starting. Each is now stopped only by something that genuinely supersedes it.
    private Coroutine _sequenceRoutine;
    private Coroutine _fadeRoutine;

    // Multiplies the live volume during a slow-down / speed-up fade (1 = no fade). Lets those routines
    // ease the volume even though Update() re-asserts the Inspector volume every frame.
    private float _fxVolumeScale = 1f;

    // Cap the per-frame step of the slow-down / speed-up ramps. A scene load (death reload, battle entry)
    // blocks the main thread, so the next Time.unscaledDeltaTime is huge — without this cap the pitch
    // would lurch in one big step (the "choppy" jump). Capped, the ramp just continues smoothly after,
    // and because the MusicController persists across the load it never has to restart.
    private const float MaxFxStep = 1f / 30f;

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
        // Track the Inspector volume (tunable live), scaled by any active slow-down / speed-up fade.
        float v = volume * _fxVolumeScale;
        if (_introSource != null) _introSource.volume = v;
        if (_loopSource  != null) _loopSource.volume  = v;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    // WebGL suspends the WebAudio context when the canvas loses focus, and on REGAIN it can resume the
    // looping source as a SECOND node stacked on the one already playing — so the music overlays and
    // stacks every time you click off the page and back. Pausing the sources on blur (so there's no live
    // node for the resume to duplicate) and unpausing on focus keeps playback to a single node, and
    // preserves position/pitch so the death-music slow-down isn't disturbed. Editor/standalone don't do
    // this, so it's web-only. WebGL fires focus and/or pause depending on the browser — handle both.
    private void OnApplicationFocus(bool hasFocus) => SetSourcesPaused(!hasFocus);
    private void OnApplicationPause(bool paused)   => SetSourcesPaused(paused);

    private void SetSourcesPaused(bool paused)
    {
        if (_introSource != null) { if (paused) _introSource.Pause(); else _introSource.UnPause(); }
        if (_loopSource  != null) { if (paused) _loopSource.Pause();  else _loopSource.UnPause();  }
    }
#endif

    /// <summary>Plays the intro once (if any), then starts the main track looping.</summary>
    public void Play()
    {
        // A brand-new track supersedes both the old sequence and any in-progress fade.
        if (_fadeRoutine     != null) { StopCoroutine(_fadeRoutine);     _fadeRoutine     = null; }
        if (_sequenceRoutine != null) { StopCoroutine(_sequenceRoutine); _sequenceRoutine = null; }
        ResetAudioFx();   // clear any lingering slow-down (pitch / muffle) so the new track plays clean
        _sequenceRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        // Clean slate: silence BOTH sources before (re)starting. A bare Play() only stops coroutines, not
        // audio, so without this a new track's intro would layer over an old loop still sounding — and on
        // WebGL, Play() on an already-playing source stacks a SECOND node rather than restarting it. Either
        // way you'd hear the track doubled. Stopping first makes overlap impossible.
        _introSource.Stop();
        _loopSource.Stop();

        // Decompress both clips before playing. They're DecompressOnLoad and, in a build, aren't
        // decompressed yet when Awake runs — playing immediately lets the decompress overrun the start
        // so the intro slips in late and doubles up with the loop. Wait for it, but cap the wait so a
        // platform that reports load state oddly (WebGL) can never hang here and leave the track silent.
        if (intro != null) intro.LoadAudioData();
        if (loop  != null) loop.LoadAudioData();
        float waited = 0f;
        while ((Loading(intro) || Loading(loop)) && waited < 3f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        // Intro once, then the main track on loop. Looping is the AudioSource's own `loop` flag — a core
        // feature that works on every platform (editor, standalone, WebGL / itch.io). The intro and loop
        // play strictly one-after-the-other: we wait for the intro to finish and stop it before starting
        // the loop, so they can never overlap (the old "double music" bug).
        if (intro != null)
        {
            _introSource.clip = intro;
            _introSource.loop = false;
            _introSource.Play();

            // Wait the intro's known length, then hand off. Don't poll isPlaying — WebGL can leave it
            // stuck true after a non-looping clip ends, which would stall the loop from ever starting.
            yield return new WaitForSecondsRealtime(intro.length);
            _introSource.Stop(); // make sure it's done before the loop starts, so they never overlap
        }

        if (loop != null)
        {
            _loopSource.Stop();   // never stack a second loop node on top of one already playing
            _loopSource.clip = loop;
            _loopSource.loop = true;
            _loopSource.Play();
        }
    }

    private static bool Loading(AudioClip clip) =>
        clip != null && clip.loadState == AudioDataLoadState.Loading;

    /// <summary>Stops both tracks — e.g. call from an outro/credits scene.</summary>
    public void StopMusic()
    {
        // Cancel a pending intro→loop hand-off too, so a stop is final and the loop can't start afterwards.
        if (_sequenceRoutine != null) { StopCoroutine(_sequenceRoutine); _sequenceRoutine = null; }
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
        // Replace only a previous fade — the intro→loop sequence keeps running so the loop still arrives.
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(SlowToStopRoutine(Mathf.Max(0.01f, duration), stopWhenDone));
    }

    private IEnumerator SlowToStopRoutine(float duration, bool stopWhenDone)
    {
        AudioLowPassFilter lp = LowPass();

        float t = 0f;
        while (t < duration)
        {
            // Capped step (see MaxFxStep) so a scene-load frame spike can't lurch the ramp; unscaled so it
            // winds down even while paused / time-scaled.
            t += Mathf.Min(Time.unscaledDeltaTime, MaxFxStep);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)); // ease the ends for a softer wind-down

            float pitch = Mathf.Lerp(1f, 0.01f, k);            // slow the playback toward a halt
            if (_introSource != null) _introSource.pitch = pitch;
            if (_loopSource  != null) _loopSource.pitch  = pitch;

            _fxVolumeScale     = 1f - k;                       // ...and fade out, so the grindy low-pitch tail is silent
            lp.cutoffFrequency = Mathf.Lerp(22000f, 300f, k);  // muffle it as it winds down

            yield return null;
        }

        _fxVolumeScale = 0f;

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
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(SpeedUpRoutine(Mathf.Max(0.01f, duration)));
    }

    private IEnumerator SpeedUpRoutine(float duration)
    {
        AudioLowPassFilter lp = LowPass();

        float startPitch  = _loopSource != null ? _loopSource.pitch : 0.01f;
        float startVol    = _fxVolumeScale;
        float startCutoff = lp.cutoffFrequency;

        float t = 0f;
        while (t < duration)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, MaxFxStep); // capped step — smooth across a scene-load spike
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));

            float pitch = Mathf.Lerp(startPitch, 1f, k);
            if (_introSource != null) _introSource.pitch = pitch;
            if (_loopSource  != null) _loopSource.pitch  = pitch;

            _fxVolumeScale     = Mathf.Lerp(startVol, 1f, k);   // fade back in as it spins up
            lp.cutoffFrequency = Mathf.Lerp(startCutoff, 22000f, k);

            yield return null;
        }

        ResetAudioFx();
    }

    /// <summary>
    /// Cleanly fades the music to silence over <paramref name="duration"/> seconds, then stops it — a plain
    /// VOLUME fade with no pitch slowdown (unlike <see cref="SlowToStop"/>), for an ending hand-off like the
    /// intro cutscene passing to the first level. A later <see cref="PlayTrack"/>/<see cref="Play"/> starts
    /// fresh. Web-safe: it only rides _fxVolumeScale and finishes in StopMusic, so nothing is left to overlap.
    /// </summary>
    public void FadeOutAndStop(float duration = 2f)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOutRoutine(Mathf.Max(0.01f, duration)));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float startVol = _fxVolumeScale;
        float t = 0f;
        while (t < duration)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, MaxFxStep); // capped step — smooth across a scene-load spike
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            _fxVolumeScale = Mathf.Lerp(startVol, 0f, k);
            yield return null;
        }

        _fxVolumeScale = 0f;
        StopMusic();      // cancels the sequence + stops both sources, so nothing lingers into the next scene
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

    // Clears any lingering slow-down: pitch back to normal, volume fade off, and the lowpass fully open.
    private void ResetAudioFx()
    {
        if (_introSource != null) _introSource.pitch = 1f;
        if (_loopSource  != null) _loopSource.pitch  = 1f;
        if (_lowPass     != null) _lowPass.cutoffFrequency = 22000f;
        _fxVolumeScale = 1f;
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
