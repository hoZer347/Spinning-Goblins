using UnityEngine;

/// <summary>
/// Shared "has this one-shot ever run?" flag store for first-time-only behaviours. Backed by
/// PlayerPrefs, which survives across sessions on every platform — including the WebGL build (stored
/// in the browser's IndexedDB) — mirroring <see cref="TutorialProgress"/> and <see cref="ScoreUI.HighScore"/>.
///
/// Every first-time-only component keys off a unique <c>id</c>; two components sharing an id therefore
/// share the same flag. Keeping the prefix in one place means <see cref="FirstTimeOnlyBehavior"/> and
/// <see cref="FirstTimeCutsceneTrigger"/> can't drift apart.
/// </summary>
public static class FirstTimeMemory
{
    const string KeyPrefix = "FirstTimeOnly_";

    /// <summary>The PlayerPrefs key backing a given id.</summary>
    public static string Key(string id) => KeyPrefix + id;

    /// <summary>True once this id's one-time action has fired at least once, on any run.</summary>
    public static bool HasRun(string id) => PlayerPrefs.GetInt(Key(id), 0) == 1;

    /// <summary>Records that this id has now fired, persisting it immediately.</summary>
    public static void MarkRun(string id)
    {
        PlayerPrefs.SetInt(Key(id), 1);
        PlayerPrefs.Save();
    }

    /// <summary>Clears the flag so the action runs again next time (e.g. an editor reset).</summary>
    public static void Reset(string id)
    {
        PlayerPrefs.DeleteKey(Key(id));
        PlayerPrefs.Save();
    }
}
