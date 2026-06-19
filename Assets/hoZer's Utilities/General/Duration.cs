using System;
using UnityEngine;


namespace AdequateGames
{
	public struct Duration
	{
		/// <summary>
		/// Resets the clock to a given duration. If Negatice, truncates to zero
		/// </summary>
		/// <param name="newDuration"></param>
		public void Reset(float newDuration = -1.0f)
		{
			if (newDuration < 0.0f)
				startingDuration = 0.0f;
			else startingDuration = newDuration;

			current = 0.0f;
		}

		/// <summary>
		/// Updates the deltaTime, returns true if it's past that time
		/// </summary>
		/// <returns></returns>
		public bool Tick()
		{
			current += Time.deltaTime;

			return current > startingDuration;
		}

		/// <summary>
		/// How far through the duration we are, clamped to 0..1. Zero-length
		/// durations report 1 (already finished).
		/// </summary>
		public float Progress => startingDuration <= 0.0f ? 1.0f : Mathf.Clamp01(current / startingDuration);

		/// <summary>
		/// The starting duration
		/// </summary>
		public float StartingDuration => startingDuration;

		public Duration(float duration)
		{
			this.startingDuration = duration;
			current = 0.0f;
		}

		float startingDuration;
		float current;
	};
};
