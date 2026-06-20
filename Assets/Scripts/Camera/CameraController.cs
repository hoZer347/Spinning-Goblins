using UnityEngine;


namespace hoZer
{
	public class CameraController : StateMachine<CameraController>
	{
		[Header("Components")]
		[SerializeField] public PlayerController playerController;

		[Header("Settings")]
		[SerializeField] public float zoomTime = .05f;
		[SerializeField] public float zoomMoveAmount = 10f;
		[SerializeField] public float zoomAmount = 1f;
		[SerializeField] public float shakeTime = .15f;
		[SerializeField] public float shakeAmount = .5f;

		Vector3 originalPosition;
		public Vector3 OriginalPosition => originalPosition;

		protected override void OnStart()
		{
			base.OnStart();

			originalPosition = transform.position;
		}
	};
};
