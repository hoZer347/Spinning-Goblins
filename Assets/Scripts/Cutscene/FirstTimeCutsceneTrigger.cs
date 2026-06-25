using System.Collections;
using System.Collections.Generic;
using hoZer;
using hoZer.Dialogue;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// First-time-only intro cutscene for the Beeg Dwarf. The very first time this object is ever
/// introduced into a scene the dwarf drops in from above the screen down to where it was placed, then
/// "lands": a big screen shake, the heavy hitstun thud, and every other on-screen enemy is launched
/// spinning off-screen (each scoring once) before being silently removed. It then begins the
/// <see cref="BeegDwarfDialogueManager"/> cutscene, which winds the dwarf up ([StartSpin]) and finally
/// thaws everything and releases the dwarf's spin attack ([Done]).
///
/// The whole sequence runs with gameplay frozen (the camera and the dialogue manager stay live). The
/// landing spot is chosen automatically — high on the screen and away from the player — so wherever the
/// dwarf was placed or spawned, it always makes its entrance dropping in up top.
///
/// On every later encounter (this session or a future one) the gate has already fired, so the component
/// destroys itself before doing anything. Persistence is shared with <see cref="FirstTimeOnlyBehavior"/>
/// through <see cref="FirstTimeMemory"/>. With no <see cref="BeegDwarfDialogueManager"/> wired, the
/// drop + landing spectacle still plays and gameplay simply resumes (no soft-lock).
/// </summary>
public class FirstTimeCutsceneTrigger : MonoBehaviour
{
    [Tooltip("Unique name for this one-shot. Two triggers sharing an id share the same \"already ran\" flag.")]
    [SerializeField] string id = "BeegDwarfIntro";

    [Header("Cutscene")]
    [Tooltip("The dialogue manager that drives the intro. Found automatically if left empty.")]
    [SerializeField] BeegDwarfDialogueManager dialogue;

    [Header("Drop-In")]
    [Tooltip("Seconds of silence (music cut, dwarf waiting offscreen above) before it drops in.")]
    [SerializeField] float preDropDelay = 1.5f;
    [Tooltip("How long the dwarf takes to fall from above the screen down to its landing spot.")]
    [SerializeField] float dropDuration = 0.6f;
    [Tooltip("Extra world units above the screen's top edge to start the fall from.")]
    [SerializeField] float dropMargin = 2f;

    [Header("Landing Spot")]
    [Tooltip("The dwarf drops onto a random point in this vertical band of the screen (viewport Y: 0 = bottom, " +
        "1 = top). Default 0.4-0.9 keeps it out of the bottom 40% but otherwise gives plenty of leeway.")]
    [SerializeField] Vector2 landViewportY = new Vector2(0.4f, 0.9f);
    [Tooltip("Keep the landing spot at least this far from the player.")]
    [SerializeField] float landMinPlayerDistance = 8f;
    [Tooltip("Required clear radius at the landing spot — rejects points on pits, spikes, or walls (but NOT enemies).")]
    [SerializeField] float landClearRadius = 1f;

    [Header("Landing Impact")]
    [Tooltip("Camera shake magnitude when the dwarf lands — big and heavy.")]
    [SerializeField] float landingShakeAmount = 12f;
    [Tooltip("How long the landing shake lasts, in seconds.")]
    [SerializeField] float landingShakeDuration = 0.5f;
    [Tooltip("Outward speed given to enemies the landing knocks off-screen.")]
    [SerializeField] float knockSpeed = 18f;
    [Tooltip("How fast (degrees/second) knocked enemies physically spin as they fly off.")]
    [SerializeField] float knockSpin = 720f;
    [Tooltip("Points awarded for each enemy cleared by the landing (shown as a floating number).")]
    [SerializeField] int pointsPerEnemy = 100;

    [Header("Music")]
    [Tooltip("Optional one-shot intro for the song the MusicController brings up as the cutscene ends.")]
    [SerializeField] AudioClip endMusicIntro;
    [Tooltip("Main looping track brought up as the cutscene ends. The original music is cut when the " +
        "dwarf drops in.")]
    [SerializeField] AudioClip endMusicLoop;

    EnemyController _dwarf;

    // The live first-time cutscene dwarf. Only ever set when the cutscene ACTUALLY runs (never on a
    // later, already-seen Beeg), and read as a live reference so a destroyed dwarf reports null on its
    // own — the block can't get stuck, and a Beeg spawned after the cutscene has happened never registers.
    static EnemyController _cutsceneDwarf;

    /// <summary>
    /// True only while the genuine first-time cutscene dwarf is still alive — the spawners pause so
    /// nothing else appears during the boss moment, then resume the instant it dies. Because it's derived
    /// from a live reference (a destroyed object reads as null), it can never get stuck blocked, and a
    /// dwarf that spawns after the cutscene has already happened never sets it, so it never blocks.
    /// </summary>
    public static bool SpawningBlocked => _cutsceneDwarf != null;

    // The id of the cutscene currently/last running, so RescueIfInterrupted can roll back the right flag.
    static string _activeId;

    // Cutscene ids that have already played THIS session. Held in memory only (a static) — never saved —
    // so it fires once per run but is forgotten when the app/page restarts, so leaving and coming back
    // replays it. Mirrors the old per-id flag, just without the PlayerPrefs persistence.
    static readonly HashSet<string> _ranThisSession = new HashSet<string>();

    // Clear the statics at each play-session start, even with editor Domain Reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _cutsceneDwarf = null;
        _activeId      = null;
        _ranThisSession.Clear();
    }

    /// <summary>
    /// Death-during-cutscene rescue: if the intro is still mid-run (its dialogue is on screen) when the
    /// player dies — they died riiight as the Beeg Dwarf appeared — roll back the one-shot flag so the
    /// cutscene isn't lost; it plays again the next time a Beeg Dwarf spawns. (The dialogue box itself is
    /// tidied separately by PersistentDialogue, which hides on scene load.) A clean completion hides the
    /// dialogue first, so this is a no-op then.
    /// </summary>
    public static void RescueIfInterrupted()
    {
        if (string.IsNullOrEmpty(_activeId)) return;
        if (PersistentDialogue.Instance == null || !PersistentDialogue.Instance.IsShowing) return;

        _ranThisSession.Remove(_activeId);
    }

    void Start()
    {
        if (_ranThisSession.Contains(id))
        {
            // Already played this session — remove only this behaviour and DON'T block spawning (the dwarf
            // is just a normal enemy now). Leaves the GameObject and its components.
            Destroy(this);
            return;
        }

        _ranThisSession.Add(id);
        _activeId = id; // remember which flag to roll back if the player dies mid-cutscene
        _dwarf = GetComponent<EnemyController>();

        // Boss moment begins now — spawning pauses while this dwarf lives (auto-resumes when it dies,
        // since the reference goes null on destroy).
        _cutsceneDwarf = _dwarf;

        StartCoroutine(BeginCutscene());
    }

    void OnDestroy()
    {
        // Tidy the static when our dwarf goes (no-op for a later, already-seen Beeg that never set it).
        if (_cutsceneDwarf == _dwarf) _cutsceneDwarf = null;
    }

    IEnumerator BeginCutscene()
    {
        // Pick the landing spot — high on the screen, away from the player — then lift the dwarf above the
        // screen over that spot THIS frame. StartCoroutine runs synchronously up to the first yield, so this
        // happens before the first render and the dwarf never flashes at its landing spot.
        Vector3 land  = LandingSpot();   // chosen with pits ON, so it still avoids them
        Vector3 start = AboveScreen(land);

        // Switch the pit colliders OFF for the whole cutscene — re-enabled on exit (below) and, as a
        // backstop, by the dialogue's OnDisable/OnDestroy. The frozen world still runs every enemy's
        // OnPhysics (the pump calls it even while a machine is disabled), and teleporting the dwarf above
        // the screen draws a swept pit-crossing line from its old spot up to here: cross an edge pit and
        // the boss falls, is destroyed, and takes this coroutine — and the whole cutscene — down with it.
        CutscenePits.Disable();

        if (_dwarf != null)
        {
            if (_dwarf.rigidbody != null)
            {
                _dwarf.rigidbody.bodyType       = RigidbodyType2D.Kinematic;
                _dwarf.rigidbody.linearVelocity = Vector2.zero;
            }
            _dwarf.transform.position = start;
        }

        // Wait one frame so every StateMachine has run its own Start() (and set its initial state)
        // before we freeze them.
        yield return null;

        CameraController  camera = FindAnyObjectByType<CameraController>();
        PlayerController  player = FindAnyObjectByType<PlayerController>();

        // Reveal the dialogue object. Prefer the persistent (DontDestroyOnLoad) one and SHOW it; otherwise
        // find any manager in the scene — INCLUDING a hidden/inactive one, which a default find would skip —
        // and make sure it's active so it can stream. This must succeed however the scene was reached.
        if (dialogue == null && PersistentDialogue.Instance != null) dialogue = PersistentDialogue.Instance.Show();
        if (dialogue == null) dialogue = FindAnyObjectByType<BeegDwarfDialogueManager>(FindObjectsInactive.Include);
        if (dialogue != null && !dialogue.gameObject.activeInHierarchy) dialogue.gameObject.SetActive(true);

        // Hold the world still for the whole sequence. The camera and the dialogue manager stay live so
        // the drop, the landing shake, and the dialogue all still play — and so does the PLAYER, which we
        // drive through its own states instead of freezing: Stopping (brake) -> CutsceneHold -> Idle.
        CutsceneFreeze.FreezeAll(dialogue, camera, player);

        // Brake the player to a stop as the music slows down.
        if (player != null) player.SetState<St_Pl_Stopping>();

        // Wind the music down (slowing + muffling to a halt) over the pre-drop beat, instead of cutting
        // it abruptly, so the silence the dwarf drops into is earned rather than jarring.
        if (MusicController.Instance != null) MusicController.Instance.SlowToStop(preDropDelay);
        yield return new WaitForSeconds(preDropDelay);

        // Once braked, hold the player perfectly still through the drop and the whole dialogue.
        if (player != null) player.SetState<St_Pl_CutsceneHold>();

        yield return Fall(start, land);

        Land(camera);

        if (dialogue != null)
        {
            // Make sure it's actually on screen right before we drive it — nothing in the cutscene should
            // have hidden it, but a stray scene transition or a missed Show() would leave Begin() invisible.
            if (PersistentDialogue.Instance != null) PersistentDialogue.Instance.Show();
            else if (!dialogue.gameObject.activeInHierarchy) dialogue.gameObject.SetActive(true);

            dialogue.Dwarf         = _dwarf;
            dialogue.Player        = player;        // [Done] returns it to Idle when the dialogue ends
            dialogue.EndMusicIntro = endMusicIntro; // the script plays the song itself via [PlayMusic]
            dialogue.EndMusicLoop  = endMusicLoop;

            // Begin() validates its own wiring and ALWAYS re-queues the script fresh, so it streams the full
            // text no matter what state the manager was in (just shown, already parsed, half-played by a
            // prior run). It only no-ops on broken wiring — in which case IsReady stays false below.
            dialogue.Begin();                       // [Done] thaws everything when the dialogue ends
        }

        if (dialogue == null || !dialogue.IsReady)
        {
            // No usable dialogue (none found, or its wiring is broken) — resume gameplay rather than
            // soft-lock behind a frozen world and a blank box. ([Done] re-enables the pits via the
            // dialogue's OnDisable when there IS a dialogue; here there's none to hide, so do it directly.)
            CutscenePits.Enable();
            CutsceneFreeze.ThawAll();
            if (player != null) player.SetState<St_Pl_Idle>();
            PlayEndMusic();
        }
    }

    void PlayEndMusic()
    {
        if (endMusicLoop != null && MusicController.Instance != null)
            MusicController.Instance.PlayTrack(endMusicLoop, endMusicIntro);
    }

    // The spot the dwarf drops onto: a random point in the upper band of the screen (out of the bottom
    // 40%), kept clear of pits / spikes / walls and away from the player — but it MAY land on other
    // enemies, so the boss always makes its entrance up top, never on top of the player, wherever it was
    // placed or spawned. Samples points and never returns one on a hazard: it prefers a clear spot far
    // from the player, and if it can't get far it takes the clear point furthest from the player.
    Vector3 LandingSpot()
    {
        Vector3 fallback = _dwarf != null ? _dwarf.transform.position : transform.position;

        Camera cam = Camera.main;
        if (cam == null) return fallback;

        PlayerController player = FindAnyObjectByType<PlayerController>();
        Vector2 playerPos = player != null ? (Vector2)player.transform.position : (Vector2)fallback;

        float depth   = Mathf.Abs(cam.transform.position.z - fallback.z);
        int   blocked = LayerMask.GetMask("Damage", "Pits", "Obstacle"); // NOT enemies — landing on them is fine

        Vector3 best = fallback;       // give up to the (spawner-cleared) spawn spot only if nothing samples clear
        float   bestDist = -1f;

        for (int i = 0; i < 40; i++)
        {
            float vx = Random.Range(0.12f, 0.88f);
            float vy = Random.Range(landViewportY.x, landViewportY.y);

            Vector3 p = cam.ViewportToWorldPoint(new Vector3(vx, vy, depth));
            p.z = fallback.z;

            // Never land on a pit / spike / wall.
            if (Physics2D.OverlapCircle(p, landClearRadius, blocked) != null) continue;

            float d = Vector2.Distance(p, playerPos);
            if (d >= landMinPlayerDistance) return p; // ideal: clear AND clear of the player

            // Otherwise keep the clear point that's furthest from the player as a fallback.
            if (d > bestDist) { bestDist = d; best = p; }
        }

        return best;
    }

    // The point directly above the top edge of the screen from which the dwarf falls in. Lifts by the
    // dwarf's own height so even its FEET start above the edge, not just its transform pivot.
    Vector3 AboveScreen(Vector3 land)
    {
        Camera cam  = Camera.main;
        float   topY = cam != null ? cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, 0f)).y : land.y + 12f;
        return new Vector3(land.x, Mathf.Max(topY, land.y) + FeetBelowPivot() + dropMargin, land.z);
    }

    // World-space distance from the dwarf's transform pivot down to the bottom of its visible sprite
    // (falls back to the collider). Read off the live world bounds, so its 2x scale is already baked in —
    // without this the feet poke past the edge at the start of the drop.
    float FeetBelowPivot()
    {
        if (_dwarf != null && _dwarf.spriteRenderer != null)
            return Mathf.Max(0f, _dwarf.transform.position.y - _dwarf.spriteRenderer.bounds.min.y);
        if (_dwarf != null && _dwarf.collider != null)
            return Mathf.Max(0f, _dwarf.transform.position.y - _dwarf.collider.bounds.min.y);
        return 1f;
    }

    // Fall from above the screen down to the landing spot, accelerating like gravity. The body is held
    // intangible during the fall so it can't shove anything on the way down.
    IEnumerator Fall(Vector3 start, Vector3 land)
    {
        if (_dwarf == null) yield break;

        bool restoreCollider = _dwarf.collider != null && _dwarf.collider.enabled;
        if (_dwarf.collider != null) _dwarf.collider.enabled = false;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, dropDuration);
            float e = Mathf.Clamp01(t);
            _dwarf.transform.position = Vector3.Lerp(start, land, e * e); // ease-in: accelerate downward
            yield return null;
        }

        _dwarf.transform.position = land;
        if (_dwarf.collider != null) _dwarf.collider.enabled = restoreCollider;
    }

    // The dwarf slams into the arena: big shake, heavy impact sound, and every other on-screen enemy is
    // launched spinning off-screen (each scoring once) before being silently removed.
    void Land(CameraController camera)
    {
        if (camera != null) camera.Shake(landingShakeAmount, landingShakeDuration);

        // Heavy impact thud — the dwarf's own hitstun clip.
        if (_dwarf != null && _dwarf.audioSource != null && _dwarf.hitstunSound != null)
            _dwarf.audioSource.PlayOneShot(_dwarf.hitstunSound, 1f);

        Camera view = Camera.main;

        foreach (EnemyController e in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            if (e == null || e == _dwarf) continue;
            if (view != null && !OnScreen(view, e.transform.position)) continue;

            // Score the clear now — the point number pops here. The later off-screen destroy is silent,
            // so the enemy is only ever counted once.
            ScoreUI.Instance?.AddKill(pointsPerEnemy, e.transform.position);

            // Knock it away from the landed dwarf; a perfectly overlapping enemy gets a random direction.
            Vector2 dir = (Vector2)(e.transform.position - transform.position);
            if (dir.sqrMagnitude < 0.0001f) dir = Random.insideUnitCircle;

            CutsceneBounceOff.Launch(e.gameObject, dir, knockSpeed, knockSpin);
        }
    }

    static bool OnScreen(Camera cam, Vector3 worldPos)
    {
        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
    }

#if UNITY_EDITOR
    /// <summary>Editor-only: clears the persisted flag so the cutscene plays again next run.</summary>
    public void ResetPersistence() => FirstTimeMemory.Reset(id);

    [CustomEditor(typeof(FirstTimeCutsceneTrigger))]
    class FirstTimeCutsceneTriggerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var trigger = (FirstTimeCutsceneTrigger)target;

            EditorGUILayout.Space();
            bool fired = FirstTimeMemory.HasRun(trigger.id);
            EditorGUILayout.LabelField("Already invoked", fired ? "Yes" : "No");

            using (new EditorGUI.DisabledScope(!fired))
            {
                if (GUILayout.Button("Reset Persistence"))
                    trigger.ResetPersistence();
            }
        }
    }
#endif
}
