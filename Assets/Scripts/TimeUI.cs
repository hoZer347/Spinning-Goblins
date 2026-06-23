using UnityEngine;
using TMPro;


public class TimeUI : MonoBehaviour
{
	TextMeshProUGUI timeText;
	float timeElapsed = 0f;

	private void Start()
	{
		timeText = GetComponent<TextMeshProUGUI>();
	}

	void Update()
	{
		timeElapsed += Time.deltaTime;
		timeText.text = $"Time: {timeElapsed:F2}";
	}
};
