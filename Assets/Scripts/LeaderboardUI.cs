using System.Text;
using TMPro;
using UnityEngine;


/// <summary>
/// Shows the global top scores from <see cref="Leaderboard"/> on a TextMeshPro text. Refreshes
/// whenever it's enabled (e.g. a game-over or menu panel opening); call <see cref="Refresh"/> to
/// re-pull on demand. If the leaderboard isn't configured / reachable it shows a friendly message.
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private int count = 10;

    private void Awake()
    {
        if (text == null) text = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable() => Refresh();

    public void Refresh()
    {
        if (text != null) text.text = "Loading…";

        Leaderboard.FetchTop(count,
            entries =>
            {
                if (text == null) return;
                if (entries.Length == 0) { text.text = "No scores yet"; return; }

                StringBuilder sb = new StringBuilder();
                foreach (Leaderboard.Entry e in entries)
                    sb.AppendLine($"{e.rank}. {e.name}  {e.score}");
                text.text = sb.ToString();
            },
            err => { if (text != null) text.text = "Leaderboard unavailable"; });
    }
}
