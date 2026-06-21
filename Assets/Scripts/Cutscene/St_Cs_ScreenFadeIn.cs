using System;


namespace hoZer
{
	/// <summary>Fades the screen from black to clear, then proceeds.</summary>
	[Serializable]
	public class St_Cs_ScreenFadeIn : St_Cs_ScreenFadeBase
	{
		protected override float AlphaAt(float progress) => 1f - progress;
	};
};
