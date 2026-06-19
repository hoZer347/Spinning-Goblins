using System;


namespace AdequateGames
{
	// Marks a method as a dialogue command, callable from a script via [Name arg, arg, ...].
	// Pass a name to expose it under something other than the method name.
	[AttributeUsage(AttributeTargets.Method)]
	public sealed class DialogueBindingAttribute : Attribute
	{
		public readonly string Name;

		public DialogueBindingAttribute(string name = null) => Name = name;
	};
};
