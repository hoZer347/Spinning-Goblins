using TMPro;
using UnityEngine;

/// <summary>
/// Displays the persisted high score recorded by <see cref="ScoreUI"/>. Drop on a TextMeshPro
/// text (e.g. a menu / load / game-over screen); it brings up the value as soon as the scene
/// loads. Absent (no text) it's a harmless no-op.
/// </summary>
public class HighScoreUI : MonoBehaviour
{
	public static HighScoreUI Instance { get; private set; }

	TextMeshProUGUI highScoreText;

	private void Awake()
	{
		Instance = this;
		highScoreText = GetComponent<TextMeshProUGUI>();
		Refresh();
	}

	private void OnDestroy()
	{
		if (Instance == this) Instance = null;
	}

	/// <summary>Re-reads the persisted high score and renders it.</summary>
	public void Refresh()
	{
		if (highScoreText != null) highScoreText.text = $"High Score: {ScoreUI.HighScore}";
	}
}
