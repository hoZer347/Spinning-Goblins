using UnityEngine;

/// <summary>
/// Persists whether the player has finished the tutorial, so later runs can skip it. Backed by
/// PlayerPrefs, which survives across sessions on every platform — including WebGL (stored in the
/// browser's IndexedDB).
/// </summary>
public static class TutorialProgress
{
    const string Key = "TutorialCompleted";

    /// <summary>True once the player has reached the end of the tutorial at least once.</summary>
    public static bool Completed
    {
#if UNITY_EDITOR
        // In the editor we always want to start from the tutorial — the "skip if already completed"
        // mechanic should only ever kick in for the real build. (The flag is still written below, so
        // it behaves normally once built.)
        get => false;
#else
        get => PlayerPrefs.GetInt(Key, 0) == 1;
#endif
        set
        {
            PlayerPrefs.SetInt(Key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Clears the flag so the tutorial plays again (e.g. a "replay tutorial" option).</summary>
    public static void Reset() => Completed = false;
}
