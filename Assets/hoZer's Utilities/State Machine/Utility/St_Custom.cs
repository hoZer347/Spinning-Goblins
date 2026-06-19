using System;
using UnityEngine;


namespace AdequateGames
{
	[Serializable]
	public class CustomState : State
	{
		public Action<State, State> onEnter;
		public Action<State> onUpdate;
		public Action<State> onPhysics;
		public Action<State, State> onExit;

		public override void OnEnter(State lastState) => onEnter(lastState, this);
		public override void OnUpdate() => onUpdate(this);
		public override void OnPhysics() => onPhysics(this);
		public override void OnExit(State nextState) => onExit(nextState, this);
	};
};
