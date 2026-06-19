using System;
using UnityEngine;


namespace AdequateGames
{
	/// <summary>
	/// A serializable, inspector-searchable reference to a StateMachine.State.
	/// Holds a live [SerializeReference] instance so its own fields can be
	/// configured inline in the inspector, and cloned fresh at runtime.
	/// </summary>
	[Serializable]
	public class StateReference
	{
		// The configured template. The property drawer swaps its concrete type.
		[SerializeReference] private State _state;

		public Type StateType => _state?.GetType();

		/// <summary>
		/// Returns a fresh copy of the configured state, or null if none is set.
		/// A copy is returned so the serialized template is never mutated at runtime.
		/// </summary>
		public State Create()
		{
			if (_state == null)
				return null;
			State copy = (State)Activator.CreateInstance(_state.GetType());
			JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(_state), copy);

			return copy;
		}
	};
};
