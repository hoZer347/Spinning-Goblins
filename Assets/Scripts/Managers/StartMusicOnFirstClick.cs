using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Keeps the level silent until the player's first left-click, then brings up the main track (an optional
/// intro one-shot, then a forever loop) through the persistent <see cref="MusicController"/>. Used on
/// Tutorial 1: the intro cutscene fades its own song out as it hands off here, and the game stays quiet
/// until the player chooses to begin — that click starts the Boss Fight song (Start → Loop).
///
/// Idempotent: if this exact loop is already playing (e.g. we returned here through a death-reload after
/// it had started), it won't restart it. Routes through <see cref="MusicController.PlayTrack"/>, which is
/// the no-overlap path (clean stop + intro→loop hand-off) so it's safe on the WebGL build.
/// </summary>
public class StartMusicOnFirstClick : MonoBehaviour
{
    [Tooltip("Optional one-shot played once before the loop (e.g. Boss Fight Start).")]
    [SerializeField] AudioClip introClip;
    [Tooltip("The main track, looped forever once the intro finishes (e.g. Boss Fight Loop).")]
    [SerializeField] AudioClip loopClip;
    [Tooltip("Per-track volume multiplier on the global music volume — >1 makes this song louder without " +
        "touching any other track.")]
    [Range(0f, 2f)] [SerializeField] float volume = 1.3f;

    bool _started;

    void Start()
    {
        // Already playing this track (came back here via a reload after it started) — nothing to wait for.
        if (loopClip != null && MusicController.Instance != null && MusicController.Instance.loop == loopClip)
            _started = true;
    }

    void Update()
    {
        if (_started) return;

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        if (!CursorManager.PointerInWindow) return; // ignore clicks outside the game view

        _started = true;
        if (loopClip != null && MusicController.Instance != null)
            MusicController.Instance.PlayTrack(loopClip, introClip, volume);
    }
}

