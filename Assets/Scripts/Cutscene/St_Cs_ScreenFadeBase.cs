using System;
using UnityEngine;


namespace hoZer
{
	/// <summary>
	/// Shared base for the screen-fade states. Finds the scene's <see cref="ScreenFader"/> — or
	/// spawns a self-contained one on a child of this machine's GameObject — then drives its alpha
	/// over <see cref="duration"/> seconds before proceeding. Subclasses pick the direction via
	/// <see cref="AlphaAt"/>.
	/// </summary>
	[Serializable]
	public abstract class St_Cs_ScreenFadeBase : State
	{
		[SerializeField] protected float duration = 0.4f;

		protected ScreenFader fader;
		Duration timer;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);

			fader = GameObject.FindAnyObjectByType<ScreenFader>();
			if (fader == null)
			{
				GameObject host = new GameObject("ScreenFader");
				host.transform.SetParent(gameObject.transform, false);
				fader = host.AddComponent<ScreenFader>();
			}

			timer.Reset(duration);
			fader.Alpha = AlphaAt(0f);
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			bool finished = timer.Tick();
			fader.Alpha = AlphaAt(timer.Progress);

			if (finished)
				Proceed();
		}

		/// <summary>Maps fade progress (0..1) to overlay alpha. 0 = clear, 1 = black.</summary>
		protected abstract float AlphaAt(float progress);
	};
};
