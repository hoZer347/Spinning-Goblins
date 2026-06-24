using UnityEngine;
using UnityEditor;
using hoZer.Dialogue;

/// <summary>
/// Editor helper that turns a scene's Dialogue object (a BeegDwarfDialogueManager + its Canvas/text) into
/// the <c>Assets/Resources/Persistent Dialogue.prefab</c> that <see cref="PersistentDialogue"/> auto-loads
/// at startup. It wraps a clone of the selection in a "Persistent Dialogue" root, wires the references,
/// and saves the prefab — so you never have to hand-build the structure or risk breaking the UI refs.
/// </summary>
public static class PersistentDialogueBuilder
{
    const string ResourcesDir = "Assets/Resources";
    const string PrefabPath   = "Assets/Resources/Persistent Dialogue.prefab";

    [MenuItem("Tools/Build Persistent Dialogue Prefab")]
    static void Build()
    {
        GameObject dialogue = Selection.activeGameObject;

        if (dialogue == null)
        {
            EditorUtility.DisplayDialog("Build Persistent Dialogue",
                "Select the Dialogue object first (the one carrying the BeegDwarfDialogueManager, " +
                "with its Canvas and text as children).", "OK");
            return;
        }

        BeegDwarfDialogueManager manager = dialogue.GetComponent<BeegDwarfDialogueManager>();
        if (manager == null)
        {
            EditorUtility.DisplayDialog("Build Persistent Dialogue",
                $"'{dialogue.name}' has no BeegDwarfDialogueManager. Select the object that holds it.", "OK");
            return;
        }

        if (System.IO.File.Exists(PrefabPath) &&
            !EditorUtility.DisplayDialog("Build Persistent Dialogue",
                $"{PrefabPath} already exists. Overwrite it?", "Overwrite", "Cancel"))
            return;

        // Build the wrapper root that stays active (so PersistentDialogue.Awake can run) and holds a
        // CLONE of the dialogue — cloning leaves the original scene object untouched.
        GameObject root = new GameObject("Persistent Dialogue");

        GameObject dialogueClone = Object.Instantiate(dialogue);
        dialogueClone.name = dialogue.name;                 // drop the "(Clone)" suffix
        dialogueClone.transform.SetParent(root.transform, false);

        PersistentDialogue persistent  = root.AddComponent<PersistentDialogue>();
        BeegDwarfDialogueManager cloneManager = dialogueClone.GetComponent<BeegDwarfDialogueManager>();

        // Wire PersistentDialogue's private [SerializeField] references.
        SerializedObject so = new SerializedObject(persistent);
        so.FindProperty("dialogueRoot").objectReferenceValue = dialogueClone;
        so.FindProperty("dialogue").objectReferenceValue     = cloneManager;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (!AssetDatabase.IsValidFolder(ResourcesDir))
            AssetDatabase.CreateFolder("Assets", "Resources");

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);

        Object.DestroyImmediate(root); // remove the temp build root from the scene

        if (success)
        {
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[PersistentDialogue] Built {PrefabPath}. It now auto-loads (disabled) in every " +
                      $"scene at play. You can delete the original '{dialogue.name}' from this scene.", prefab);
        }
        else
        {
            EditorUtility.DisplayDialog("Build Persistent Dialogue", "Failed to save the prefab.", "OK");
        }
    }

    // Greys the menu item out unless a GameObject with a BeegDwarfDialogueManager is selected.
    [MenuItem("Tools/Build Persistent Dialogue Prefab", isValidateFunction: true)]
    static bool BuildValidate() =>
        Selection.activeGameObject != null &&
        Selection.activeGameObject.GetComponent<BeegDwarfDialogueManager>() != null;
}
