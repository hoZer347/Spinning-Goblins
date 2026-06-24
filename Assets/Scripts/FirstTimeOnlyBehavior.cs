using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Runs an action exactly once, ever — the very first time this object is introduced into a scene.
/// After it fires it records that fact to local persistent memory, so on every later encounter (this
/// session or any future one, after the game is rebooted) the component destroys itself before doing
/// anything. Only the behaviour is destroyed, never the GameObject it lives on.
///
/// Persistence is backed by <see cref="FirstTimeMemory"/> (PlayerPrefs under the hood), which survives
/// across sessions on every platform — including the WebGL build (stored in the browser's IndexedDB) —
/// mirroring <see cref="TutorialProgress"/> and <see cref="ScoreUI.HighScore"/>.
///
/// Give each instance a unique <see cref="id"/> so separate one-shots don't share the same flag.
/// Hook up what should happen the first time via the <see cref="onFirstTime"/> event in the inspector.
/// </summary>
public class FirstTimeOnlyBehavior : MonoBehaviour
{
    [Tooltip("Unique name for this one-shot. The persisted flag is keyed on it, so two behaviours " +
             "sharing an id share the same \"already ran\" state. Keep it stable once shipped.")]
    [SerializeField] string id = "";

    [Tooltip("Invoked once, the very first time this object is ever introduced into a scene.")]
    [SerializeField] UnityEvent onFirstTime;

    void Awake()
    {
        if (string.IsNullOrEmpty(id))
            Debug.LogWarning($"[FirstTimeOnlyBehavior] '{name}' has no id — its flag collides with " +
                             "every other un-named first-time behaviour. Give it a unique id.", this);

        if (FirstTimeMemory.HasRun(id))
        {
            // Seen before: tear down just this behaviour, leaving the GameObject (and its other
            // components) untouched, and do nothing else.
            Destroy(this);
            return;
        }

        // First encounter: remember it, then run the one-time action.
        FirstTimeMemory.MarkRun(id);
        onFirstTime?.Invoke();
    }

#if UNITY_EDITOR
    /// <summary>Editor-only: clears the persisted flag so this acts as if it had never run.</summary>
    public void ResetPersistence() => FirstTimeMemory.Reset(id);

    [CustomEditor(typeof(FirstTimeOnlyBehavior))]
    class FirstTimeOnlyBehaviorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var behavior = (FirstTimeOnlyBehavior)target;

            EditorGUILayout.Space();
            bool fired = FirstTimeMemory.HasRun(behavior.id);
            EditorGUILayout.LabelField("Already invoked", fired ? "Yes" : "No");

            using (new EditorGUI.DisabledScope(!fired))
            {
                if (GUILayout.Button("Reset Persistence"))
                    behavior.ResetPersistence();
            }
        }
    }
#endif
}
