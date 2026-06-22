using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class TutorialText1 : MonoBehaviour
{
	TextMeshProUGUI textMesh;

	private void Start()
	{
		textMesh = GetComponentInChildren<TextMeshProUGUI>();
	}

	private void Update()
	{
		if (Mouse.current.leftButton.wasPressedThisFrame)
		{
			textMesh.text = "Click again to slow down!";
		};
	}
}
