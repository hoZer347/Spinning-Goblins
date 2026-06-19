using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;


namespace AdequateGames
{
	/// <summary>
	/// Adds "Create > Adequate Games > State" to the Project window, spawning a new
	/// St_ state script pre-filled from StateScriptTemplate.cs.txt. #SCRIPTNAME# is
	/// replaced with whatever the user types when naming the asset.
	/// </summary>
	public static class CreateStateMenu
	{
		// Resolved from this script's own folder, so the templates are found wherever this
		// file (and the .cs.txt files beside it) get moved.
		static readonly string editorDir = ScriptDir();

		static readonly string templatePath         = $"{editorDir}/StateScriptTemplate.cs.txt";
		static readonly string machineTemplatePath  = $"{editorDir}/StateMachineScriptTemplate.cs.txt";
		static readonly string dialogueTemplatePath = $"{editorDir}/DialogueManagerScriptTemplate.cs.txt";

		// This file's directory as a project-relative "Assets/..." path. CallerFilePath is
		// the absolute compile-time path; we keep the part from "Assets/" onward.
		static string ScriptDir([CallerFilePath] string thisFile = "")
		{
			string dir = Path.GetDirectoryName(thisFile).Replace('\\', '/');
			int    i   = dir.IndexOf("/Assets/", StringComparison.Ordinal);

			return i >= 0 ? dir.Substring(i + 1) : dir;
		}

		[MenuItem("Assets/Create/Adequate Games/State", false, 80)]
		static void CreateState()
		{
			ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, "St_NewState.cs");
		}

		// Greys the menu entry out if the template file is missing rather than
		// silently creating an empty script.
		[MenuItem("Assets/Create/Adequate Games/State", true)]
		static bool CreateStateValidate()
		{
			return AssetDatabase.LoadAssetAtPath<TextAsset>(templatePath) != null;
		}

		// #SCRIPTNAME# fills both the class name and the StateMachine<...> focus, so the
		// built-in template replacement is enough (no custom action needed).
		[MenuItem("Assets/Create/Adequate Games/State Machine", false, 81)]
		static void CreateStateMachine()
		{
			ProjectWindowUtil.CreateScriptAssetFromTemplateFile(machineTemplatePath, "NewStateMachine.cs");
		}

		[MenuItem("Assets/Create/Adequate Games/State Machine", true)]
		static bool CreateStateMachineValidate()
		{
			return AssetDatabase.LoadAssetAtPath<TextAsset>(machineTemplatePath) != null;
		}

		// Creates a DialogueManager subclass with an example [DialogueBinding] method.
		[MenuItem("Assets/Create/Adequate Games/Dialogue", false, 82)]
		static void CreateDialogue()
		{
			ProjectWindowUtil.CreateScriptAssetFromTemplateFile(dialogueTemplatePath, "NewDialogueManager.cs");
		}

		[MenuItem("Assets/Create/Adequate Games/Dialogue", true)]
		static bool CreateDialogueValidate()
		{
			return AssetDatabase.LoadAssetAtPath<TextAsset>(dialogueTemplatePath) != null;
		}

		// Scaffolds a focus-typed state script using the same inline-rename flow as the
		// Create menu, typed to the state machine's focus and dropped into the same folder
		// (and namespace) as the machine's own script. Called by the StateReference UI.
		public static void CreateFocusedState(UnityEngine.Object machine)
		{
			TextAsset template = AssetDatabase.LoadAssetAtPath<TextAsset>(templatePath);

			if (template == null || machine == null)
				return;

			Type   focusType = machine.GetType();
			string folder    = ScriptFolder(machine);

			if (string.IsNullOrEmpty(folder))
				return;

			DoCreateFocusedState action = ScriptableObject.CreateInstance<DoCreateFocusedState>();
			action.template = template.text;
			action.focus    = focusType.Name;
			action.ns       = focusType.Namespace;

			Texture2D icon = EditorGUIUtility.IconContent("cs Script Icon").image as Texture2D;

			ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
				0,
				action,
				$"{folder}/St_New{focusType.Name}State.cs",
				icon,
				null);
		}

		// The folder of the machine's own script, so the new state is created beside it.
		static string ScriptFolder(UnityEngine.Object machine)
		{
			MonoScript script = machine is MonoBehaviour mb ? MonoScript.FromMonoBehaviour(mb) : null;
			string     path   = script != null ? AssetDatabase.GetAssetPath(script) : null;

			return string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path).Replace('\\', '/');
		}
	};

	// Finishes the inline-named creation started by CreateFocusedState, substituting
	// the typed class name and focus into the State template.
	class DoCreateFocusedState : EndNameEditAction
	{
		public string template;
		public string focus;
		public string ns;

		public override void Action(int instanceId, string pathName, string resourceFile)
		{
			string className = Path.GetFileNameWithoutExtension(pathName);

			string content = template
				.Replace("#SCRIPTNAME# : State", $"#SCRIPTNAME# : State<{focus}>")
				.Replace("#SCRIPTNAME#", className);

			// Match the machine's namespace so the short State<focus> reference resolves
			// (sub-namespaces of AdequateGames still see the base State type).
			if (!string.IsNullOrEmpty(ns))
				content = content.Replace("namespace AdequateGames", $"namespace {ns}");

			File.WriteAllText(pathName, content);

			AssetDatabase.ImportAsset(pathName);

			UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(pathName);

			ProjectWindowUtil.ShowCreatedAsset(asset);
		}
	};
};
