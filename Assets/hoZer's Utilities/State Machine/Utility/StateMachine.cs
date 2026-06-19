using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using System;
using System.Reflection;
#endif


namespace AdequateGames
{
	abstract public class StateMachine<_Focus> : StateMachine
		where _Focus : StateMachine<_Focus>, new()
	{ };

	abstract public class StateMachine : MonoBehaviour
	{
		/// <summary>
		/// Initial State at the start of the GameObject's lifetime
		/// </summary>
		[SerializeField] StateReference _startState;

		State current;
		public State Current => current;

		State disabledState = null;

		float elapsedTime = 0f;

		#region State Management

		/// <summary>
		/// Pushes a new state to the bottom of the queue and returns it, then proceeds to it.
		/// </summary>
		/// <typeparam name="_State"></typeparam>
		/// <returns></returns>
		public _State SetState<_State>()
			where _State : State, new()
		{
			var state = new _State();

			PushFirst(state);
			Proceed();

			return state;
		}

		/// <summary>
		/// Pushes a new state to the back of the queue and returns it, then proceeds to it.
		/// </summary>
		/// <typeparam name="_State"></typeparam>
		/// <param name="state"></param>
		/// <returns></returns>
		public _State SetState<_State>(_State state)
			where _State : State
		{
			PushFirst(state);
			Proceed();
			return state;
		}

		/// <summary>
		/// Shortcut for PushParallel + Proceed
		/// </summary>
		/// <param name="states"></param>
		/// <returns></returns>
		public State[] SetParallel(params State[] states)
		{
			var ret = PushParallel(states);

			Proceed();

			return ret;
		}

		/// <summary>
		/// Shortcut for PushParallel + Proceed
		/// </summary>
		/// <param name="states"></param>
		/// <returns></returns>
		public State[] SetParallel(IEnumerable<State> states)
		{
			var ret = PushParallel(states);

			Proceed();

			return ret;
		}

		/// <summary>
		/// Pushes a new state to the top of the stack and returns it.
		/// </summary>
		/// <typeparam name="_State"></typeparam>
		/// <returns></returns>
		public _State Push<_State>()
			where _State : State, new()
		{
			_stateQueue.Add(new _State());

			return _stateQueue.Last() as _State;
		}

		/// <summary>
		/// Pushes a new state to the top of the stack and returns it.
		/// </summary>
		/// <typeparam name="_State"></typeparam>
		/// <param name="state"></param>
		/// <returns></returns>
		public _State Push<_State>(_State state)
			where _State : State
		{
			_stateQueue.Add(state);

			return _stateQueue.Last() as _State;
		}

		/// <summary>
		/// Pushes a new state to the bottom of the stack and returns it.
		/// </summary>
		/// <typeparam name="_State"></typeparam>
		/// <returns></returns>
		public _State PushFirst<_State>()
			where _State : State, new()
		{
			_stateQueue.Insert(0, new _State());

			return _stateQueue.First() as _State;
		}

		/// <summary>
		/// Pushes a new state to the bottom of the stack and returns it.
		/// </summary>
		/// <typeparam name="_State"></typeparam>
		/// <param name="state"></param>
		/// <returns></returns>
		public _State PushFirst<_State>(_State state)
			where _State : State
		{
			_stateQueue.Insert(0, state);

			return _stateQueue.First() as _State;
		}

		/// <summary>
		/// Pushes a number of states in Parallel and returns them.
		/// </summary>
		/// <param name="states"></param>
		/// <returns></returns>
		public State[] PushParallel(params State[] states)
		{
			St_Parallel parallelState = new St_Parallel(states);
			
			_stateQueue.Add(parallelState);

			return states;
		}

		/// <summary>
		/// Pushes a number of states in Parallel and returns them.
		/// </summary>
		/// <param name="states"></param>
		/// <returns></returns>
		public State[] PushParallel(IEnumerable<State> states)
		{
			var parallelState = new St_Parallel(states);

			_stateQueue.Add(parallelState);

			return states.ToArray();
		}

		/// <summary>
		/// Pushes a number of states in Parallel to the start of the Queue.
		/// </summary>
		/// <param name="states"></param>
		/// <returns></returns>
		public State[] PushParallelFirst(params State[] states)
		{
			St_Parallel parallelState = new St_Parallel(states);
			
			_stateQueue.Insert(0, parallelState);

			return states;
		}

		/// <summary>
		/// Pushes a number of states in Parallel to the start of the Queue.
		/// </summary>
		/// <param name="states"></param>
		/// <returns></returns>
		public State[] PushParallelFirst(IEnumerable<State> states)
		{
			var parallelState = new St_Parallel(states);

			_stateQueue.Insert(0, parallelState);

			return states.ToArray();
		}

		/// <summary>
		/// Removes the current state from the stack and returns the new current state.
		/// </summary>
		/// <typeparam name="_State"></typeparam>
		/// <returns></returns>
		public virtual State Proceed()
		{
			// St_Parallel overrides normal Proceed
			if (current is St_Parallel parallel && parallel.OnChildProceed())
				return _stateQueue.Count > 0 ? _stateQueue.First() : null;

			State lastCurrent = _stateQueue.Count > 0 ? _stateQueue.First() : null;

			if (Current != null)
				Current.OnExit(lastCurrent);

#if UNITY_EDITOR
			if (_stateQueue.Count > 0)
				_stateStack.Enqueue((elapsedTime, _stateQueue.First()));

			if (_stateStack.Count > maxSavedStates)
				_stateStack.Dequeue();
#endif

			if (_stateQueue.Count > 0)
			{
				current = _stateQueue.First();
				current.gameObject = gameObject;
				current.stateMachine = this;

				if (current is St_Parallel)
					foreach (var state in (current as St_Parallel).SubStates)
					{
						state.gameObject = gameObject;
						state.stateMachine = this;
					};

				current.OnEnter(lastCurrent);

				_stateQueue.RemoveAt(0);
			};

			return _stateQueue.Count > 0 ? _stateQueue.First() : null;
		}

		/// <summary>
		/// Removes all states from the stack.
		/// </summary>
		public void Clear() => _stateQueue.Clear();

		/// <summary>
		/// Disables the State by saving the current state and then running null until enabled
		/// </summary>
		public void Disable()
		{
			disabledState = current;
			current = null;
		}

		/// <summary>
		/// Enables a state by reinstating the state disabled
		/// </summary>
		public void Enable() => current = disabledState;

		#endregion

		#region ParallelState

		/// <summary>
		/// Handler for many Sub-States running at the same time: 
		/// Does not Proceed until all Sub-States inside have triggered Proceed 
		/// (Note: States will still trigger OnExit as they call Proceed, not all at once)
		/// </summary>
		protected sealed class St_Parallel : State
		{
			public St_Parallel(IEnumerable<State> states) : base()
				=> _subStates = new(states);

			/// <summary>
			/// Sub-States that all run at once
			/// </summary>
			[SerializeField, HideInInspector] List<State> _subStates;

			/// <summary>
			/// Sub-States that all run at once
			/// </summary>
			public List<State> SubStates => _subStates;

			/// <summary>
			/// Currently active Sub-State
			/// </summary>
			State _active;

			/// <summary>
			/// Handles individual Sub-States calling Proceed
			/// </summary>
			/// <returns></returns>
			public bool OnChildProceed()
			{
				if (_active == null || !_subStates.Remove(_active))
					return false;

				State finished = _active;
				_active = null;

				finished.OnExit(null);

				return _subStates.Count > 0;
			}

			/// <summary>
			/// Runs OnUpdate all Sub-States, keeping track of the current one being processed
			/// </summary>
			/// <param name="lastState">The Previous State</param>
			public override void OnEnter(State lastState)
			{
				foreach (var state in _subStates.ToArray())
				{
					_active = state;
					state?.OnEnter(lastState);
					_active = null;
				};
			}

			/// <summary>
			/// Runs OnUpdate all Sub-States, keeping track of the current one being processed
			/// </summary>
			public override void OnUpdate()
			{
				foreach (var state in _subStates.ToArray())
				{
					_active = state;
					state?.OnUpdate();
					_active = null;
				};
			}

			/// <summary>
			/// Runs OnPhysics all Sub-States, keeping track of the current one being processed
			/// </summary>
			public override void OnPhysics()
			{
				foreach (var state in _subStates.ToArray())
				{
					_active = state;
					state?.OnPhysics();
					_active = null;
				};
			}

			/// <summary>
			/// Runs OnExit all Sub-States, keeping track of the current one being processed
			/// </summary>
			/// <param name="lastState">The Next State</param>
			public override void OnExit(State nextState)
			{
				foreach (var state in _subStates.ToArray())
					state?.OnExit(nextState);
			}
		};

		#endregion

		#region Logging

#if UNITY_EDITOR
		/// <summary>
		/// A stack of previously active states for debugging purposes.
		/// The maximum number of states saved can be set in the inspector.
		/// </summary>
		Queue<(float, State)> _stateStack = new();

		/// <summary>
		/// The amount of state history to track before discarding old ones. Keep low or zero unless
		/// debugging to avoid memory over-use.
		/// </summary>
		[SerializeField] int maxSavedStates = 0;

		/// <summary>
		/// Serializes every recorded past state (oldest first) to text and clears the
		/// history. Editor-only debug aid, driven by the inspector button.
		/// </summary>
		public string FlushStateHistory()
		{
			System.Text.StringBuilder builder = new();

			while (_stateStack.Count > 0)
			{
				(float time, State state) = _stateStack.Dequeue();

				if (state == null)
					continue;

				builder.AppendLine($"[{time:0.000}] {state.GetType().Name}: {JsonUtility.ToJson(state)}");
			};

			return builder.ToString();
		}
#endif

		#endregion

		#region Private Methods

		/// <summary>
		/// Queue of states. On Proceed, pulls the first element.
		/// </summary>
		List<State> _stateQueue = new();

		protected virtual void OnStart() { }

		protected virtual void OnGizmos() { }

		protected virtual void OnPhysics() { }

		protected virtual void OnUpdate() { }

		void Start()
		{
			OnStart();

			State state = _startState?.Create();

			if (state != null)
				SetState(state);
		}

		void Update()
		{
			elapsedTime += Time.deltaTime;
			OnUpdate();
			Current?.OnUpdate();
		}

		void FixedUpdate()
		{
			OnPhysics();
			Current?.OnPhysics();
		}

		void OnDrawGizmos()
		{
			OnGizmos();
			Current?.OnGizmos();
		}

#if UNITY_EDITOR
		static readonly string[] reservedMessages = { "Start", "Update", "FixedUpdate", "OnDrawGizmos", "OnValidate" };

		private void OnValidate()
		{
			for (Type type = GetType(); type != null && type != typeof(StateMachine); type = type.BaseType)
				foreach (MethodInfo method in type.GetMethods(
					BindingFlags.Instance
					| BindingFlags.Public
					| BindingFlags.NonPublic
					| BindingFlags.DeclaredOnly))
					if (Array.IndexOf(reservedMessages, method.Name) >= 0)
						Debug.LogError(
							$"{ type.Name } declares '{ method.Name }()', which hides StateMachine's Unity "
							+ "message and stops the state pump. Override OnStart / OnUpdate / OnPhysics / "
							+ "OnGizmos / OnValidate.",
							this);
		}
#endif

		#endregion
	};
};
