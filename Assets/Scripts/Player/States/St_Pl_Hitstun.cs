using System;
using UnityEngine;


namespace hoZer
{
	[Serializable]
	public class St_Pl_Hitstun : St_Pl_Base
	{
		Duration timeUntilDone;
		Duration flashDuration;
		bool flashState;

		public override void OnEnter(State lastState)
		{
			base.OnEnter(lastState);
			
			timeUntilDone.Reset(Focus.hitStunTime);
			flashDuration.Reset(Focus.hurtFlashTime);
			SfxManager.Play(Focus.gobHurt, .5f);
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			if (timeUntilDone.Tick())
			{
				SetState<St_Pl_Stopping>();
				return;
			};

			if (flashDuration.Tick())
			{
				flashDuration.Reset(Focus.hurtFlashTime);
				Focus.Sprite.enabled = (flashState = !flashState);
			};
		}

		public override void OnExit(State nextState)
		{
			base.OnExit(nextState);

			Focus.Sprite.enabled = true;
		}
	};
};
