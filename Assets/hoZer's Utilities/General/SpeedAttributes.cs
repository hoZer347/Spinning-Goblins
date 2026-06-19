using System;
using UnityEngine;


namespace AdequateGames
{
	[Serializable]
	public struct SpeedAttributes
	{
		#region Variables

		[SerializeField] public float	minSpeed;
		[SerializeField] public float	maxSpeed;
		[SerializeField] public float	acceleration;
		[SerializeField] public float	deceleration;
		[SerializeField] public float	friction;
		[SerializeField] public Vector3	direction;

		public float gravity { get => deceleration; set => deceleration = value; }

		float _speed;
		public float Speed => _speed;
		public Vector3 SpeedVector => direction.normalized * _speed;

		#endregion

		#region Methods

		public void Reset(float speed = .0f) => _speed = speed;

		public void Accelerate()
		{
			_speed += acceleration * Time.deltaTime;
			if (_speed > maxSpeed)
				_speed = maxSpeed;
		}

		public void Decelerate()
		{
			_speed -= deceleration * Time.deltaTime;
			if (_speed < minSpeed)
				_speed = minSpeed;
		}

		public void ApplyFriction()
		{
			float frictionDelta = friction * Time.deltaTime;

			if (Math.Abs(_speed) < frictionDelta)
				_speed = 0;

			if (_speed < 0.0f)
				_speed += frictionDelta;

			if (_speed > 0.0f)
				_speed -= frictionDelta;
		}

		public void ApplyTo(Transform transform)
		{
			transform.position += SpeedVector;
		}

		#endregion
	};
};
