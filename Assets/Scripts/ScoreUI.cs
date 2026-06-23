using TMPro;
using UnityEngine;

/// <summary>
/// Score display and scoring hub. One instance lives in the gameplay scene (on the Score text).
/// Gameplay code reports events through the static <see cref="Instance"/>; this tallies and renders.
/// Absent (e.g. in scenes with no score text), every call is a harmless no-op via the null check.
/// </summary>
public class ScoreUI : MonoBehaviour
{
	public static ScoreUI Instance { get; private set; }

	TextMeshProUGUI scoreText;
	int _score;
	int _flyingCombo; // hits landed during the current St_Pl_Flying state

	// Add-pulse tuning kept as consts (not serialized fields): adding serialized fields here trips
	// the "script class layout is incompatible between editor and player" error on IL2CPP/WebGL.
	const float PulseScale = 1.3f;
	const float PulseReturnSpeed = 8f;
	Vector3 _baseScale = Vector3.one;

	public int Score => _score;

	const string HighScoreKey = "HighScore";

	/// <summary>The best score ever recorded, persisted across runs via PlayerPrefs.</summary>
	public static int HighScore
	{
		get => PlayerPrefs.GetInt(HighScoreKey, 0);
		private set
		{
			PlayerPrefs.SetInt(HighScoreKey, value);
			PlayerPrefs.Save();
		}
	}

	private void Awake()
	{
		Instance = this;
		scoreText = GetComponent<TextMeshProUGUI>();
		_baseScale = transform.localScale;
		Refresh();
	}

	private void Update()
	{
		// Ease the score text back to its base scale after an Add pulse.
		transform.localScale = Vector3.Lerp(transform.localScale, _baseScale, PulseReturnSpeed * Time.deltaTime);
	}

	private void OnDestroy()
	{
		if (Instance == this) Instance = null;
	}

	public void Add(int score)
	{
		_score += score;
		if (_score > HighScore) HighScore = _score; // record new highs as they happen
		Pulse();
		Refresh();
	}

	/// <summary>Snap to the pulse scale; Update eases it back, giving a pop on each Add.</summary>
	private void Pulse() => transform.localScale = _baseScale * PulseScale;

	private void Refresh()
	{
		if (scoreText != null) scoreText.text = $"Score: {_score}";
	}

	// --- Scoring events -----------------------------------------------------------------------

	/// <summary>Start of a flight: restart the consecutive-hit combo.</summary>
	public void ResetFlyingCombo() => _flyingCombo = 0;

	/// <summary>A flying-player hit on an enemy: 10, then +10 per consecutive hit this flight.</summary>
	public void AddFlyingHit() => Add(++_flyingCombo * 10);

	/// <summary>An enemy knocked into the damage layer: 20 per point of damage dealt.</summary>
	public void AddHazardDamage(int damage) => Add(20 * damage);

	/// <summary>A normal enemy kill.</summary>
	public void AddKill() => Add(100);

	/// <summary>A kill by knockback into a pit: 100 + 20 per remaining HP.</summary>
	public void AddPitKill(int remainingHealth) => Add(100 + 20 * remainingHealth);
}
