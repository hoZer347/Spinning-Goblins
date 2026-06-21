using System;


namespace hoZer
{
	/// <summary>Fades the screen from clear to black, then proceeds.</summary>
	[Serializable]
	public class St_Cs_ScreenFadeOut : St_Cs_ScreenFadeBase
	{
		protected override float AlphaAt(float progress) => progress;
	};
};
