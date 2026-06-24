using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global one-shot SFX router that keeps many sounds firing at once from summing into a wall of noise.
/// Two safeguards: a per-clip hard cap drops excess copies of the SAME clip fired in a short window (no
/// machine-gun flutter), and a global "crowd duck" makes every new sound quieter the more voices are
/// already sounding — so a chain reaction (hits + spins + pit-falls together) stays controlled. A sound
/// or two played together is left at full volume; only the crowd beyond <c>FreeVoices</c> ducks.
///
/// Backed by a small pool of 2D AudioSources on a persistent object; the first <see cref="Play"/> call
/// spawns the manager, so there's no scene setup. Route any SFX that can fire many-at-once through it
/// (hits, spins, pit-falls); single, deliberate one-offs can keep using a plain AudioSource.
/// </summary>
public class SfxManager : MonoBehaviour
{
    [Tooltip("Identical clips fired within this window count toward the same stack.")]
    const float StackWindow = 0.08f;
    [Tooltip("Never play more than this many copies of one clip within a window — extras are dropped.")]
    const int   MaxStack    = 4;
    [Tooltip("How hard the mix ducks as more voices sound at once. Higher = stronger duck. 0 = off.")]
    const float CrowdFactor = 0.6f;
    [Tooltip("This many simultaneous voices play at full volume; only the crowd beyond it ducks — so " +
             "normal play (a spin or two overlapping) stays full while a chain reaction gets reined in.")]
    const int   FreeVoices  = 2;
    const int   PoolSize    = 24;

    static SfxManager _instance;
    static bool       _quitting;

    static SfxManager Instance
    {
        get
        {
            if (_instance == null && !_quitting)
            {
                GameObject go = new GameObject("SfxManager");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<SfxManager>();
                _instance.BuildPool();
            }
            return _instance;
        }
    }

    AudioSource[] _pool;
    int           _next;
    readonly Dictionary<AudioClip, float> _windowStart = new();
    readonly Dictionary<AudioClip, int>   _windowHits  = new();

    void BuildPool()
    {
        _pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake   = false;
            src.spatialBlend  = 0f; // 2D — full volume wherever the emitter sits
            _pool[i] = src;
        }
    }

    void OnApplicationQuit() => _quitting = true;

    /// <summary>
    /// Plays a 2D one-shot, throttled so simultaneous / rapid copies of the same clip don't pile up in
    /// volume. Returns silently for a null clip or after the app has begun quitting.
    /// </summary>
    public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        SfxManager m = Instance;
        if (m != null) m.PlayInternal(clip, volume, pitch);
    }

    void PlayInternal(AudioClip clip, float volume, float pitch)
    {
        float now = Time.unscaledTime;

        // Open a fresh stack window for this clip if the last one has elapsed.
        if (!_windowStart.TryGetValue(clip, out float start) || now - start > StackWindow)
        {
            _windowStart[clip] = now;
            _windowHits[clip]  = 0;
        }

        int hits = _windowHits[clip];
        if (hits >= MaxStack) return;          // already enough copies this window — drop the extras
        _windowHits[clip] = hits + 1;

        // Global crowd duck: past a few free voices, the more SFX already sounding the quieter each new
        // one — so a burst of many sounds at once (a chain of hits, several spins, a pile of pit-falls)
        // stays controlled instead of summing into a wall of noise. A sound or two played together is
        // unaffected; only the crowd beyond FreeVoices ducks.
        int   crowd  = Mathf.Max(0, ActiveVoiceCount() - FreeVoices);
        float scaled = volume / Mathf.Sqrt(1f + crowd * CrowdFactor);

        AudioSource src = _pool[_next];
        _next = (_next + 1) % _pool.Length;
        src.pitch = pitch;
        src.PlayOneShot(clip, scaled);
    }

    // How many pooled voices are currently sounding — a live proxy for how crowded the SFX mix is.
    int ActiveVoiceCount()
    {
        int n = 0;
        for (int i = 0; i < _pool.Length; i++)
            if (_pool[i].isPlaying) n++;
        return n;
    }
}
