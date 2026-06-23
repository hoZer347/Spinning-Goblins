using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;


/// <summary>
/// Global high-score leaderboard backed by LootLocker (https://lootlocker.com), talking to its REST
/// API directly over HTTPS via UnityWebRequest — so it works in the itch.io WebGL build with no SDK
/// package. It signs in as a guest (a stable per-browser identity stored in PlayerPrefs), submits
/// scores, and fetches the top N.
///
/// It bootstraps itself before the first scene and survives scene loads, so any script can call the
/// static <see cref="Submit"/> / <see cref="FetchTop"/> from anywhere. If the keys below are blank
/// it simply disables itself (every call becomes a no-op), so the game still runs offline.
///
/// SETUP (one time):
///   1. Make a free account at lootlocker.com and create a game.
///   2. Copy the game's API key into <see cref="ApiKey"/> (use the prod key for the live build).
///   3. Under Leaderboards, create one of type "Generic"; copy its KEY into <see cref="LeaderboardKey"/>.
///   4. Make sure <see cref="GameVersion"/> matches a game version in your LootLocker settings.
/// </summary>
public class Leaderboard : MonoBehaviour
{
    // ===================== PASTE YOUR LOOTLOCKER VALUES HERE =====================
    const string ApiKey         = "";         // LootLocker game API key (e.g. dev_xxxxxxxx / prod_xxxxxxxx)
    const string GameVersion    = "0.1.0.0";  // must match a version configured in your LootLocker game
    const string LeaderboardKey = "";         // the leaderboard's "key" (a generic-type leaderboard)
    // ============================================================================

    const string Base = "https://api.lootlocker.io/game";
    const string PlayerIdKey = "ll_player_identifier";

    public static Leaderboard Instance { get; private set; }

    /// <summary>One row of the leaderboard, already cleaned up for display.</summary>
    public struct Entry { public int rank; public string name; public int score; }

    string _sessionToken;
    string _memberId;
    bool   _signingIn;

    static bool Configured => !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(LeaderboardKey);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        if (!Configured)
        {
            Debug.LogWarning("[Leaderboard] ApiKey / LeaderboardKey not set — global high scores are disabled.");
            return;
        }

        GameObject go = new GameObject("[Leaderboard]");
        DontDestroyOnLoad(go);
        go.AddComponent<Leaderboard>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        StartCoroutine(SignIn());
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // --- Public API ---------------------------------------------------------------------------

    /// <summary>Submits a score for this player. Safe to call when unconfigured (no-op).</summary>
    public static void Submit(string playerName, int score)
    {
        if (Instance == null) return;
        Instance.StartCoroutine(Instance.SubmitRoutine(playerName, score));
    }

    /// <summary>Fetches the top <paramref name="count"/> entries, then invokes a callback on the main thread.</summary>
    public static void FetchTop(int count, Action<Entry[]> onResult, Action<string> onError = null)
    {
        if (Instance == null) { onError?.Invoke("Leaderboard not configured."); return; }
        Instance.StartCoroutine(Instance.FetchRoutine(count, onResult, onError));
    }

    // --- Session ------------------------------------------------------------------------------

    IEnumerator SignIn()
    {
        _signingIn = true;

        // A stable identity so this browser keeps the same leaderboard entry across visits.
        string identifier = PlayerPrefs.GetString(PlayerIdKey, "");
        if (string.IsNullOrEmpty(identifier))
        {
            identifier = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(PlayerIdKey, identifier);
            PlayerPrefs.Save();
        }

        string body = JsonUtility.ToJson(new GuestBody
        {
            game_key = ApiKey,
            game_version = GameVersion,
            player_identifier = identifier
        });

        using (UnityWebRequest req = Json($"{Base}/v2/session/guest", "POST", body, auth: false))
        {
            yield return req.SendWebRequest();
            if (IsOk(req))
            {
                GuestResponse resp = JsonUtility.FromJson<GuestResponse>(req.downloadHandler.text);
                _sessionToken = resp.session_token;
                _memberId     = resp.public_uid;
            }
            else
            {
                Debug.LogWarning($"[Leaderboard] sign-in failed: {req.error} {Text(req)}");
            }
        }

        _signingIn = false;
    }

    IEnumerator EnsureSession()
    {
        if (!string.IsNullOrEmpty(_sessionToken)) yield break;
        if (_signingIn) { while (_signingIn) yield return null; }
        else yield return SignIn();
    }

    // --- Routines -----------------------------------------------------------------------------

    IEnumerator SubmitRoutine(string playerName, int score)
    {
        yield return EnsureSession();
        if (string.IsNullOrEmpty(_sessionToken)) yield break;

        string body = JsonUtility.ToJson(new SubmitBody
        {
            member_id = _memberId,
            score     = score,
            metadata  = string.IsNullOrEmpty(playerName) ? "Anon" : playerName
        });

        using (UnityWebRequest req = Json($"{Base}/leaderboards/{LeaderboardKey}/submit", "POST", body))
        {
            yield return req.SendWebRequest();
            if (!IsOk(req))
                Debug.LogWarning($"[Leaderboard] submit failed: {req.error} {Text(req)}");
        }
    }

    IEnumerator FetchRoutine(int count, Action<Entry[]> onResult, Action<string> onError)
    {
        yield return EnsureSession();
        if (string.IsNullOrEmpty(_sessionToken)) { onError?.Invoke("No session."); yield break; }

        using (UnityWebRequest req = Json($"{Base}/leaderboards/{LeaderboardKey}/list?count={count}", "GET", null))
        {
            yield return req.SendWebRequest();
            if (!IsOk(req)) { onError?.Invoke(req.error); yield break; }

            ListResponse resp = JsonUtility.FromJson<ListResponse>(req.downloadHandler.text);
            int n = resp.items != null ? resp.items.Length : 0;
            Entry[] entries = new Entry[n];
            for (int i = 0; i < n; i++)
            {
                Item it = resp.items[i];
                string name = !string.IsNullOrEmpty(it.metadata) ? it.metadata
                            : (it.player != null && !string.IsNullOrEmpty(it.player.name) ? it.player.name : "Anon");
                entries[i] = new Entry { rank = it.rank, name = name, score = it.score };
            }
            onResult?.Invoke(entries);
        }
    }

    // --- Helpers ------------------------------------------------------------------------------

    UnityWebRequest Json(string url, string method, string body, bool auth = true)
    {
        UnityWebRequest req = new UnityWebRequest(url, method)
        {
            downloadHandler = new DownloadHandlerBuffer()
        };
        if (body != null)
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));

        req.SetRequestHeader("Content-Type", "application/json");
        if (auth && !string.IsNullOrEmpty(_sessionToken))
            req.SetRequestHeader("x-session-token", _sessionToken);
        return req;
    }

    static bool IsOk(UnityWebRequest req) => req.result == UnityWebRequest.Result.Success;
    static string Text(UnityWebRequest req) => req.downloadHandler != null ? req.downloadHandler.text : "";

    // --- JSON DTOs ----------------------------------------------------------------------------

    [Serializable] class GuestBody     { public string game_key; public string game_version; public string player_identifier; }
    [Serializable] class GuestResponse { public string session_token; public string public_uid; }
    [Serializable] class SubmitBody    { public string member_id; public int score; public string metadata; }
    [Serializable] class ListResponse  { public Item[] items; }
    [Serializable] class Item          { public int rank; public int score; public string metadata; public Player player; }
    [Serializable] class Player        { public string name; }
}
