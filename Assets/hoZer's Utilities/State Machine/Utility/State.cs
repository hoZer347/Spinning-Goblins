using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace AdequateGames
{
	public class State<_Focus> : State
	where _Focus : StateMachine<_Focus>, new()
	{
		protected _Focus Focus => stateMachine as _Focus;
	};

	[Serializable]
	public class State
	{
		[HideInInspector] public GameObject gameObject;
		[HideInInspector] public StateMachine stateMachine;

		#region Overrideables

		// Real class virtuals (not default interface methods), so subclasses override
		// them and the compiler enforces a matching signature. Dispatch through a
		// State / State<T> reference hits the override.

		/// <summary>
		/// Gets called when we call Proceed and the state enters the scene, works like a
		/// start function on a MonoBehviour
		/// </summary>
		/// <param name="lastState"></param>
		public virtual void OnEnter(State lastState) { }

		/// <summary>
		/// Gets called as the StateMachine's Monobehaviour Update function
		/// </summary>
		public virtual void OnUpdate() { }

		/// <summary>
		/// Gets called as the StateMachine's Monobehaviour FixedUpdate function
		/// </summary>
		public virtual void OnPhysics() { }

		/// <summary>
		/// Gets called as the StateMachine's MonoBehaviour OnDrawGizmos function
		/// </summary>
		public virtual void OnGizmos() { }

		/// <summary>
		/// Get called when we call Proceed and exit this state
		/// </summary>
		/// <param name="nextState"></param>
		public virtual void OnExit(State nextState) { }

		#endregion

		#region StateMachine Bindings

		/// <summary>
		/// See StateMachine implementation
		/// </summary>
		/// <typeparam name="_State"></typeparam>
		protected virtual _State SetState<_State>()
			where _State : State, new()
				=> stateMachine.SetState<_State>();

		/// <summary>
		/// See StateMachine implementation
		/// </summary>
		/// <typeparam name="_State"></typeparam>
		/// <param name="state"></param>
		protected virtual _State SetState<_State>(_State state)
			where _State : State
				=> stateMachine.SetState(state);

		/// <summary>
		/// See StateMachine implementation
		/// </summary>
		/// <param name="states"></param>
		/// <returns></returns>
		protected virtual State[] SetParallel(params State[] states)
			=> stateMachine.SetParallel(states);

		/// <summary>
		/// See StateMachine implementation
		/// </summary>
		/// <typeparam name="_State"></typeparam>
		protected virtual _State Push<_State>()
			where _State : State, new()
				=> stateMachine.Push<_State>();

		/// <summary>
		/// See StateMachine implementation
		/// </summary>
		/// <typeparam name="_State"></typeparam>
		/// <param name="state"></param>
		protected virtual _State Push<_State>(_State state)
			where _State : State
				=> stateMachine.Push(state);

		/// <summary>
		/// See StateMachine implementation
		/// </summary>
		/// <typeparam name="_State"></typeparam>
		/// <returns></returns>
		protected virtual _State PushFirst<_State>()
			where _State : State, new()
				=> stateMachine.PushFirst<_State>();

		/// <summary>
		/// See StateMachine implementation
		/// </summary>
		/// <typeparam name="_State"></typeparam>
		/// <param name="state"></param>
		protected virtual _State PushFirst<_State>(_State state)
			where _State : State =>
				stateMachine.PushFirst(state);

		/// <summary>
		/// See StateMachine implementation
		/// </summary>
		/// <param name="states"></param>
		protected virtual State[] PushParallel(params State[] states)
			=> stateMachine.PushParallel(states);

		/// <summary>
		/// See StateMachine implementation
		/// </summary>
		/// <param name="states"></param>
		protected virtual State[] PushParallel(IEnumerable<State> states)
			=> stateMachine.PushParallel(states);

		/// <summary>
		/// See StateMachine implementation
		/// </summary>
		/// <param name="states"></param>
		protected virtual State[] PushParallelFirst(params State[] states)
			=> stateMachine.PushParallelFirst(states);

		/// <summary>
		/// See StateMachine implementation
		/// </summary>
		/// <param name="states"></param>
		protected virtual State[] PushParallelFirst(IEnumerable<State> states)
			=> stateMachine.PushParallelFirst(states);

		/// <summary>
		/// See StateMachine implementation
		/// </summary>
		protected virtual void Proceed() => stateMachine.Proceed();

		/// <summary>
		/// See StateMachine implementation
		/// </summary>
		protected virtual void Clear() => stateMachine.Clear();

		#endregion
	};
};
