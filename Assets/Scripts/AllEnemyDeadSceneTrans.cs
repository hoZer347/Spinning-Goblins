using hoZer;
using Tymski;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AllEnemyDeadSceneTrans : MonoBehaviour
{
	[SerializeField] SceneReference nextScene;
	[SerializeField] float delayUntilTrans;
	Duration duration;

	private void Start()
	{
		duration.Reset(delayUntilTrans);
	}

	private void Update()
	{
		EnemyController enemyControllers = FindAnyObjectByType<EnemyController>();
		if (enemyControllers == null)
		{
			if (duration.Tick())
			{
				// Always route through GameManager so CurrentLevelIndex stays in sync.
				if (GameManager.Instance != null)
				{
					Debug.Log($"[AllEnemyDeadSceneTrans] All enemies dead — calling GameManager.LoadNextLevel (currentIndex={GameManager.Instance.CurrentLevelIndex})");
					GameManager.Instance.LoadNextLevel();
				}
				else if (nextScene != null && !string.IsNullOrEmpty(nextScene.ScenePath))
				{
					Debug.Log($"[AllEnemyDeadSceneTrans] All enemies dead — no GameManager, loading nextScene: {nextScene.ScenePath}");
					SceneManager.LoadScene(nextScene.ScenePath);
				}
				else
				{
					Debug.LogError("[AllEnemyDeadSceneTrans] No GameManager and no nextScene assigned.", this);
				}
			}
		}
		else duration.Reset(delayUntilTrans);
	}
};
