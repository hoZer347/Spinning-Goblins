using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;


namespace AdequateGames
{
	[CustomPropertyDrawer(typeof(StateReference))]
	public class StateReferenceDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			SerializedProperty stateProp = property.FindPropertyRelative("_state");

			// Header line: label + searchable type dropdown + a "New" button.
			Rect line = position;
			line.height = EditorGUIUtility.singleLineHeight;

			Rect contentRect = EditorGUI.PrefixLabel(line, label);

			const float buttonWidth = 44f;
			const float spacing = 2f;

			Rect dropdownRect = new(
				contentRect.x,
				contentRect.y,
				contentRect.width - buttonWidth - spacing,
				contentRect.height);

			Rect newButtonRect = new(
				dropdownRect.xMax + spacing,
				contentRect.y,
				buttonWidth,
				contentRect.height);

			// Only offer states whose focus is this state machine, so e.g. a
			// CameraStats machine doesn't list St_Pl2D_* (State<Player2D>) states.
			UnityEngine.Object machine = property.serializedObject.targetObject;
			Type machineType = machine?.GetType();

			if (EditorGUI.DropdownButton(dropdownRect, new GUIContent(DisplayName(stateProp)), FocusType.Keyboard))
			{
				StateDropdown dropdown = new(new AdvancedDropdownState(), machineType);

				dropdown.OnPicked = picked =>
				{
					stateProp.managedReferenceValue = picked == null
						? null
						: Activator.CreateInstance(picked);

					property.serializedObject.ApplyModifiedProperties();
				};

				dropdown.OnCreateNew = () => CreateStateMenu.CreateFocusedState(machine);

				dropdown.Show(dropdownRect);
			};

			// Scaffolds a new focus-typed state script, but (unlike picking <New> in the
			// dropdown context) leaves the start state untouched.
			if (GUI.Button(newButtonRect, "New"))
				CreateStateMenu.CreateFocusedState(machine);

			// Body: the selected state's own serialized fields, drawn without
			// the default managed-reference foldout header (the dropdown is our header).
			if (stateProp.managedReferenceValue != null)
			{
				float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

				EditorGUI.indentLevel++;

				foreach (SerializedProperty child in Children(stateProp))
				{
					float h = EditorGUI.GetPropertyHeight(child, true);

					EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), child, true);

					y += h + EditorGUIUtility.standardVerticalSpacing;
				};

				EditorGUI.indentLevel--;
			};
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			float height = EditorGUIUtility.singleLineHeight;

			SerializedProperty stateProp = property.FindPropertyRelative("_state");

			if (stateProp.managedReferenceValue != null)
			{
				height += EditorGUIUtility.standardVerticalSpacing;

				foreach (SerializedProperty child in Children(stateProp))
					height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
			};

			return height;
		}

		// Iterates the immediate visible children of a property (the state's fields).
		private static IEnumerable<SerializedProperty> Children(SerializedProperty property)
		{
			SerializedProperty iterator = property.Copy();
			SerializedProperty end = iterator.GetEndProperty();

			bool enterChildren = true;

			while (iterator.NextVisible(enterChildren)
				&& !SerializedProperty.EqualContents(iterator, end))
			{
				enterChildren = false;

				yield return iterator.Copy();
			};
		}

		private static string DisplayName(SerializedProperty stateProp)
		{
			object value = stateProp.managedReferenceValue;

			return value == null
				? "<None>"
				: value.GetType().Name;
		}
	};

	/// <summary>
	/// AdvancedDropdown ships with a search field, giving us the searchable picker for free.
	/// </summary>
	public class StateDropdown : AdvancedDropdown
	{
		public Action<Type> OnPicked;
		public Action OnCreateNew;

		private readonly List<Type> _types;

		public StateDropdown(AdvancedDropdownState state, Type machineType) : base(state)
		{
			minimumSize = new Vector2(0f, 240f);

			_types = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(SafeGetTypes)
				.Where(t =>
				{
					Type focus = StateFocus(t);

					return focus != null
						&& !t.IsAbstract
						&& !t.IsGenericType
						&& t.GetConstructor(Type.EmptyTypes) != null
						// Keep states whose focus is (a base of) this machine. When the
						// owner isn't a typed machine, fall back to listing everything.
						&& (machineType == null || focus.IsAssignableFrom(machineType));
				})
				.OrderBy(t => t.Name)
				.ToList();
		}

		// The focus (T) of the State<T> a type derives from, or null for plain State
		// subclasses (e.g. ParallelState / St_Parallel), which we skip.
		private static Type StateFocus(Type type)
		{
			for (Type baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
				if (baseType.IsGenericType
					&& baseType.GetGenericTypeDefinition() == typeof(State<>))
					return baseType.GetGenericArguments()[0];

			return null;
		}

		protected override AdvancedDropdownItem BuildRoot()
		{
			AdvancedDropdownItem root = new("States");

			root.AddChild(new StateItem("<None>", null));
			root.AddChild(new StateItem("<New>", null, true));

			foreach (Type type in _types)
				root.AddChild(new StateItem(type.Name, type));

			return root;
		}

		protected override void ItemSelected(AdvancedDropdownItem item)
		{
			if (item is not StateItem stateItem)
				return;

			if (stateItem.CreateNew)
				OnCreateNew?.Invoke();
			else
				OnPicked?.Invoke(stateItem.Type);
		}

		private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
		{
			try
			{
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				return e.Types.Where(t => t != null);
			};
		}

		private class StateItem : AdvancedDropdownItem
		{
			public Type Type { get; }
			public bool CreateNew { get; }

			public StateItem(string name, Type type, bool createNew = false) : base(name)
			{
				Type = type;
				CreateNew = createNew;
			}
		};
	};
};
