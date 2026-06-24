using UnityEngine;
using hoZer.Dialogue;

/// <summary>
/// Keeps the Beeg Dwarf intro dialogue alive across every scene, hidden until it's needed. One instance
/// is spawned from Resources before the first scene loads, marks itself DontDestroyOnLoad, and disables
/// the dialogue UI so it sits dormant in the background of whatever scene the game starts in.
///
/// <see cref="FirstTimeCutsceneTrigger"/> calls <see cref="Show"/> to reveal and drive it when the dwarf
/// first spawns; the cutscene's [Done] binding calls <see cref="Hide"/> to put it away again. It is never
/// destroyed — re-hiding leaves it ready (and consistent) for later scenes.
///
/// Structure of the prefab (Assets/Resources/Persistent Dialogue.prefab):
///   • Root  — always active, carries THIS component.
///       • Dialogue subtree (Canvas + BeegDwarfDialogueManager + text boxes) — assigned to
///         <see cref="dialogueRoot"/>; this is what gets toggled. The manager is <see cref="dialogue"/>.
/// The root must stay active so this Awake can run; only the dialogue child is toggled.
/// </summary>
public class PersistentDialogue : MonoBehaviour
{
    public static PersistentDialogue Instance { get; private set; }

    [Tooltip("The dialogue UI subtree (Canvas + manager) toggled on only during the cutscene. Starts hidden.")]
    [SerializeField] GameObject dialogueRoot;
    [Tooltip("The dialogue manager that drives the cutscene — lives under dialogueRoot.")]
    [SerializeField] BeegDwarfDialogueManager dialogue;

    // Resources path of the prefab auto-loaded at startup (this component lives on its root).
    const string ResourcePath = "Persistent Dialogue";

    // Spawn the one persistent instance before any scene's objects wake, so it's present no matter which
    // scene the game is launched from (the editor's "play from this scene" included).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[PersistentDialogue] No prefab at Resources/{ResourcePath} — the Beeg Dwarf " +
                             "intro dialogue won't appear. Create it and place it under a Resources folder.");
            return;
        }

        Instantiate(prefab);
    }

    void Awake()
    {
        // Singleton: the first instance persists; any later copy (a scene that also holds one) removes
        // itself so we never double up.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);   // be a root object so DontDestroyOnLoad reliably persists us
        DontDestroyOnLoad(gameObject);

        Hide();                      // dormant in every scene until the cutscene shows it
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Reveals the dialogue UI and returns its manager so the caller can drive it.</summary>
    public BeegDwarfDialogueManager Show()
    {
        if (dialogueRoot != null) dialogueRoot.SetActive(true);
        return dialogue;
    }

    /// <summary>Hides the dialogue UI again, leaving the persistent object intact for later scenes.</summary>
    public void Hide()
    {
        if (dialogueRoot != null) dialogueRoot.SetActive(false);
    }
}
