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
				SceneManager.LoadScene(nextScene.ScenePath);
		}
		else duration.Reset(delayUntilTrans);
	}
};
