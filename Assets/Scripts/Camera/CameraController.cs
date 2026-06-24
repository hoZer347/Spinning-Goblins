using UnityEngine;


namespace hoZer
{
	public class CameraController : StateMachine<CameraController>
	{
		[Header("Components")]
		[HideInInspector] public PlayerController playerController;

		[Header("Settings")]
		[SerializeField] public float zoomTime = .05f;
		[SerializeField] public float zoomMoveAmount = 10f;
		[SerializeField] public float zoomAmount = 1f;
		[SerializeField] public float shakeTime = .15f;
		[SerializeField] public float shakeAmount = 5f;

		Vector3 originalPosition;
		public Vector3 OriginalPosition => originalPosition;

		/// <summary>
		/// Triggers a one-off shake with a custom magnitude / duration — e.g. the big Beeg Dwarf landing
		/// impact. Leaves the serialized defaults (used by the normal hit shake) untouched.
		/// </summary>
		public void Shake(float amount, float time) => SetState(new St_Cm_Shake(amount, time));

		protected override void OnStart()
		{
			base.OnStart();

			originalPosition = transform.position;

			if (playerController == null)
				playerController = FindAnyObjectByType<PlayerController>();
		}
	};
};
