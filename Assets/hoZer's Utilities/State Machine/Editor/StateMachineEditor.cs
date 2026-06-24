using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;


namespace hoZer
{
	[CustomEditor(typeof(StateMachine), true)]
	[CanEditMultipleObjects]
	public class StateMachineEditor : Editor
	{
		static readonly HashSet<string> baseFields = BuildBaseFields();

		public override void OnInspectorGUI()
		{
			StateMachine machine = (StateMachine)target;

			serializedObject.Update();

			bool drewSubclass = false;

			SerializedProperty it = serializedObject.GetIterator();
			bool enterChildren = true;

			while (it.NextVisible(enterChildren))
			{
				enterChildren = false;

				if (baseFields.Contains(it.name))
					continue;

				EditorGUILayout.PropertyField(it, true);
				drewSubclass = true;
			};

			if (drewSubclass)
			{
				EditorGUILayout.Space();
				HorizontalLine();
				EditorGUILayout.Space();
			}

			using (new EditorGUI.DisabledScope(true))
			{
				// With several machines selected the current state may differ between them, so
				// show a mixed-value dash unless they all report the same state.
				string current = CurrentStateName(machine);
				bool mixed = false;

				for (int i = 1; i < targets.Length; i++)
					if (CurrentStateName((StateMachine)targets[i]) != current)
					{
						mixed = true;

						break;
					};

				EditorGUI.showMixedValue = mixed;
				EditorGUILayout.TextField("Current State", mixed ? "—" : current);
				EditorGUI.showMixedValue = false;
			};

			it = serializedObject.GetIterator();
			enterChildren = true;

			while (it.NextVisible(enterChildren))
			{
				enterChildren = false;

				if (it.name == "m_Script" || !baseFields.Contains(it.name))
					continue;

				EditorGUILayout.PropertyField(it, true);
			};

			serializedObject.ApplyModifiedProperties();

			EditorGUILayout.Space();

			if (GUILayout.Button("Flush State History To Text"))
				foreach (Object obj in targets)
					FlushToText((StateMachine)obj);
		}

		public override bool RequiresConstantRepaint() => Application.isPlaying;

		static HashSet<string> BuildBaseFields()
		{
			HashSet<string> set = new() { "m_Script" };

			for (System.Type type = typeof(StateMachine); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
				foreach (FieldInfo field in type.GetFields(
					BindingFlags.Instance
					| BindingFlags.Public
					| BindingFlags.NonPublic
					| BindingFlags.DeclaredOnly))
					set.Add(field.Name);

			return set;
		}

		static void HorizontalLine()
		{
			Rect rect = EditorGUILayout.GetControlRect(false, 1);
			EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f, 1f));
		}

		static string CurrentStateName(StateMachine machine)
		{
			if (!Application.isPlaying)
				return "(play mode only)";

			return machine.Current?.GetType().Name ?? "<None>";
		}

		static void FlushToText(StateMachine machine)
		{
			string text = machine.FlushStateHistory();

			if (string.IsNullOrEmpty(text))
			{

				return;
			};

			if (!AssetDatabase.IsValidFolder("Assets/Logs"))
				AssetDatabase.CreateFolder("Assets", "Logs");

			string path = $"Assets/Logs/{machine.gameObject.name}_StateHistory.txt";

			File.WriteAllText(path, text);
			AssetDatabase.ImportAsset(path);

			Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);

			EditorGUIUtility.PingObject(asset);

		}
	};
};
