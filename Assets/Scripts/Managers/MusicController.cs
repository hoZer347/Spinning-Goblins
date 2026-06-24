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

    [Header("Tracks")]
    [Tooltip("Optional one-shot played once before the loop. Leave empty to start looping immediately.")]
    public AudioClip intro;
    [Tooltip("The main track — looped forever once the intro finishes.")]
    public AudioClip loop;

    [Header("Playback")]
    [Range(0f, 1f)] public float volume = 0.6f;

    private AudioSource _introSource;
    private AudioSource _loopSource;

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
        // Small lead-in so both scheduled starts are armed before the DSP clock reaches them.
        double startTime = AudioSettings.dspTime + 0.1;

        if (intro != null)
        {
            _introSource.clip = intro;
            _introSource.PlayScheduled(startTime);

            double introLength = (double)intro.samples / intro.frequency;
            if (loop != null)
            {
                _loopSource.clip = loop;
                _loopSource.PlayScheduled(startTime + introLength);
            }
        }
        else if (loop != null)
        {
            _loopSource.clip = loop;
            _loopSource.PlayScheduled(startTime);
        }
    }

    /// <summary>Stops both tracks — e.g. call from an outro/credits scene.</summary>
    public void StopMusic()
    {
        if (_introSource != null) _introSource.Stop();
        if (_loopSource  != null) _loopSource.Stop();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
