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
				// An explicitly assigned nextScene wins: this transition points straight at the
				// next scene, self-contained. Only when no scene is assigned do we defer to the
				// GameManager, which drives endless-mode level progression.
				string scene = nextScene != null ? nextScene.ScenePath : null;
				if (!string.IsNullOrEmpty(scene))
				{
					SceneManager.LoadScene(scene);
				}
				else if (GameManager.Instance != null)
				{
					GameManager.Instance.LoadNextLevel();
				}
				else
				{
					Debug.LogError("[AllEnemyDeadSceneTrans] nextScene resolved to an empty path — " +
						"re-assign it in the Inspector and confirm the target scene is in Build Settings. " +
						"(Tymski SceneReference can serialize empty into a build.)", this);
				}
			}
		}
		else duration.Reset(delayUntilTrans);
	}
};
