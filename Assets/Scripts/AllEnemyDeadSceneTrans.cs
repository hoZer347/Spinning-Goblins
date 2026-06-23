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
				string scene = nextScene != null ? nextScene.ScenePath : null;
				if (string.IsNullOrEmpty(scene))
					Debug.LogError("[AllEnemyDeadSceneTrans] nextScene resolved to an empty path — " +
						"re-assign it in the Inspector and confirm the target scene is in Build Settings. " +
						"(Tymski SceneReference can serialize empty into a build.)", this);
				else
					SceneManager.LoadScene(scene);
			}
		}
		else duration.Reset(delayUntilTrans);
	}
};
