using UnityEngine;


namespace hoZer.Dialogue
{
	public class Buh : DialogueManager
	{
		// Callable from a dialogue script as: [ExampleBinding, Alice, 3, 1.5]
		// Comma-separated: the binding name first, then one arg per parameter.
		[DialogueBinding]
		void ExampleBinding(string speaker, int count, float speed)
		{
			Debug.Log($"ExampleBinding -> speaker: { speaker }, count: { count }, speed: { speed }");
		}
	};
};
