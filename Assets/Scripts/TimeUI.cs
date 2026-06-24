using UnityEngine;
using TMPro;


public class TimeUI : MonoBehaviour
{
	/// <summary>The level's timer display, so gameplay systems can read the same clock the player sees.</summary>
	public static TimeUI Instance { get; private set; }

	TextMeshProUGUI timeText;
	float timeElapsed = 0f;

	/// <summary>Seconds elapsed since the level began — the value shown on the timer.</summary>
	public float TimeElapsed => timeElapsed;

	private void Awake()
	{
		Instance = this;
	}

	private void OnDestroy()
	{
		if (Instance == this) Instance = null;
	}

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
