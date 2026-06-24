using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using TMPro;

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace hoZer.Dialogue
{
	/// <summary>
	/// A StateMachie that manages the parsing and display of a Dialogue Script
	/// </summary>
	[Serializable]
	public class DialogueManager : StateMachine<DialogueManager>
	{
		/// <summary>
		/// The script from which this DialogueManager will Parse and Display
		/// </summary>
		[SerializeField] TextAsset _script;

		/// <summary>
		/// The Font this DialogueManager will start with.
		/// </summary>
		[SerializeField] Font _startingFont;

		/// <summary>
		/// The text streaming speed this DialogueManager will start with, in seconds per character.
		/// </summary>
		[SerializeField] float _startingTextSpeed = .03f;

		/// <summary>
		/// The TextMeshProUGUI component that this DialogueManager will stream text to.
		/// </summary>
		[SerializeField] TextMeshProUGUI _dialogueBox;

		/// <summary>
		/// The TextMeshProUGUI component that this DialogueManager will use to prompt the player to continue when the dialogue is paused.
		/// </summary>
		[SerializeField] TextMeshProUGUI _continueText;

		[Header("Voice (Banjo-Kazooie style)")]
		[Tooltip("Short blip played, pitched randomly, as each character streams in. Leave empty for silent text.")]
		[SerializeField] AudioClip _voiceClip;
		[Range(0f, 1f)] [SerializeField] float _voiceVolume = 0.5f;
		[Tooltip("Random pitch range for each blip — the wider the gap, the more babble-like the voice.")]
		[SerializeField] float _voicePitchMin = 0.85f;
		[SerializeField] float _voicePitchMax = 1.25f;
		[Tooltip("Play a blip every Nth visible character (1 = every character). Raise it to thin out fast text.")]
		[Min(1)] [SerializeField] int _voiceEvery = 2;

		// Dedicated runtime source so the per-blip pitch changes never bend any other audio. Created in
		// OnStart only when a clip is assigned. Counter paces the "every Nth character" blips.
		AudioSource _voiceSource;
		int         _voiceCharCount;

		/// <summary>
		/// The TextMeshProUGUI component that this DialogueManager will stream text to.
		/// </summary>
		public TextMeshProUGUI TextMesh => _dialogueBox;

		/// <summary>
		/// The TextMeshProUGUI component that this DialogueManager will use to prompt the player to continue when the dialogue is paused.
		/// </summary>
		public TextMeshProUGUI ContinueText => _continueText;

		/// <summary>
		/// The text speed for this DialogueManager, in seconds per character. This can be changed by the script using the [TextSpeed] binding.
		/// </summary>
		float _textSpeed;

		/// <summary>
		/// The text speed for this DialogueManager, in seconds per character. This can be changed by the script using the [TextSpeed] binding.
		/// </summary>
		public float textSpeed => _textSpeed;

		/// <summary>
		/// Trigger this to begin the dialogue.
		/// </summary>
		public void Begin() => Proceed();

		/// <summary>
		/// Plays one randomly-pitched voice blip for a streamed character — the Banjo-Kazooie babble.
		/// Whitespace is silent, and only every Nth visible character blips. No-op until a clip is assigned.
		/// </summary>
		public void Speak(char c)
		{
			if (_voiceClip == null || _voiceSource == null || char.IsWhiteSpace(c))
				return;

			// Pace the blips: sound on every _voiceEvery-th visible character.
			if (_voiceCharCount++ % Mathf.Max(1, _voiceEvery) != 0)
				return;

			_voiceSource.pitch = UnityEngine.Random.Range(_voicePitchMin, _voicePitchMax);
			_voiceSource.PlayOneShot(_voiceClip, _voiceVolume);
		}

		#region Default Bindings

		// These are the default Mappings from Dialogue text
		// for example:
		// - [Pause] maps to Pause()
		// - [TextSpeed, 0.1f] will set the text speed to 0.1f

		/// <summary>
		/// Proceeds to the next state in the queue.
		/// </summary>
		[DialogueBinding] new void Proceed() => base.Proceed();

		/// <summary>
		/// Clears the dialogue box of all text.
		/// </summary>
		[DialogueBinding] new void Clear() => _dialogueBox.text = "";

		/// <summary>
		/// Pauses the dialogue and prompts the player to press a key to continue.
		/// </summary>
		[DialogueBinding] void Pause() => PushFirst<St_Dg_WaitForInput>();
		
		/// <summary>
		/// Sets the text speed for the dialogue.
		/// </summary>
		[DialogueBinding] void TextSpeed(float speed) => _textSpeed = speed;

		/// <summary>
		/// Sets the text speed for the dialogue based on a preset string. Valid options are
		/// "Slow", "Normal", "Fast", and "Default" (which resets to the starting text speed).
		/// </summary>
		/// <param name="speed">The preset string representing the desired text speed.</param>
		[DialogueBinding] void TextSpeed(string speed)
		{
			switch (speed)
			{
				case "Slow":	_textSpeed = _startingTextSpeed * 2;	break;
				case "Normal":	_textSpeed = _startingTextSpeed;		break;
				case "Fast":	_textSpeed = _startingTextSpeed / 2;	break;
				case "Default":	_textSpeed = _startingTextSpeed;		break;
				default:
					Debug.LogWarning($"[Dialogue] Unrecognized text speed '{ speed }'.");
					break;
			};
		}

		#endregion

		#region Binding Management

		/// <summary>
		/// Regex to split the script into tokens, which can be either:
		/// [Binding Name, Arg1, Arg2, ...] command,
		/// or TMPro rich text features like <b></b> markup
		/// or Plain Text in between.
		/// </summary>
		static readonly Regex TokenRegex = new Regex(@"(\[[^\]]*\]|<[^>]*>)");

		const BindingFlags MethodFlags =
			BindingFlags.Instance
			| BindingFlags.Static
			| BindingFlags.Public
			| BindingFlags.NonPublic;

		// binding name -> method, built from this manager's [DialogueBinding] functions.
		readonly Dictionary<string, MethodInfo> _bindings = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Disables this DialogueManager and pings it in the editor to alert the developer of a bad setup.
		/// </summary>
		void DisableFromBadSetup()
		{
			Disable();

#if UNITY_EDITOR
			Selection.activeObject = gameObject;
			EditorGUIUtility.PingObject(gameObject);
#endif
		}

		/// <summary>
		/// Initializes Bindings and Queues States based on the provided script.
		/// </summary>
		protected override void OnStart()
		{
			#region Setup Checks

			if (_dialogueBox == null)
			{
				Debug.LogError("[Dialogue] - Dialogue Box is not assigned.");
				DisableFromBadSetup();
				return;
			};

			if (_continueText == null)
			{
				Debug.LogError("[Dialogue] - Continue Text is not assigned.");
				DisableFromBadSetup();
				return;
			};

			if (_script == null)
			{
				Debug.LogError("[Dialogue] - Script is not assigned.");
				DisableFromBadSetup();
				return;
			};

			if (_startingFont == null)
			{
				Debug.LogError("[Dialogue] - Starting Font not set.");
				DisableFromBadSetup();
				return;
			}

			#endregion

			#region Initializing Bindings and Queueing States

			base.OnStart();

			_textSpeed = _startingTextSpeed;

			_dialogueBox.text = string.Empty;

			_continueText.gameObject.SetActive(false);

			// A dedicated 2D source for the streaming voice blips, so their random per-blip pitch never
			// bends any other audio. Only created when a voice clip is assigned.
			if (_voiceClip != null && _voiceSource == null)
			{
				_voiceSource = gameObject.AddComponent<AudioSource>();
				_voiceSource.playOnAwake  = false;
				_voiceSource.spatialBlend = 0f; // 2D — full volume regardless of position
			}
			_voiceCharCount = 0;

			// Collect this manager's type chain down to DialogueManager. Walking the hierarchy with
			// DeclaredOnly is what makes inheritance work: GetMethods does not surface a base class's
			// PRIVATE bindings for a derived instance, so a subclass would otherwise silently lose the
			// defaults (Pause / TextSpeed / ...).
			List<Type> chain = new();
			for (Type type = GetType(); type != null && typeof(DialogueManager).IsAssignableFrom(type); type = type.BaseType)
				chain.Add(type);

			// Register base-first so a derived [DialogueBinding] overrides a base one of the same name,
			// while keeping the original "last declared wins" among same-name overloads within a type.
			for (int i = chain.Count - 1; i >= 0; i--)
				foreach (MethodInfo method in chain[i].GetMethods(MethodFlags | BindingFlags.DeclaredOnly))
				{
					DialogueBindingAttribute attr = method.GetCustomAttribute<DialogueBindingAttribute>();

					if (attr == null)
						continue;

					_bindings[attr.Name ?? method.Name] = method;
				};

			string[] splits = TokenRegex.Split(_script.text);

			foreach (string split in splits)
			{
				// Skip useless blankspace
				if (string.IsNullOrWhiteSpace(split))
					continue;

				// Process Bindings
				if (split.StartsWith("[") && split.EndsWith("]"))
					Push<St_Dg_InvokeBinding>().binding = () => InvokeBinding(split.Substring(1, split.Length - 2));

				// Process Rich Text Entries
				else if (split.StartsWith("<") && split.EndsWith(">"))
					Push<St_Dg_StreamRichText>().text = split;

				// Process Plain Text
				else Push<St_Dg_StreamPlainText>().text = split;
			};

			Push<St_Dg_DialogueEnd>();

			#endregion
		}

		/// <summary>
		/// Invokes a Binding based on the provided content string "[BindingName, Arg1, Arg2, ...]".
		/// </summary>
		/// <param name="content"></param>
		void InvokeBinding(string content)
		{
			int comma = content.IndexOf(',');

			string name    = (comma < 0 ? content : content.Substring(0, comma)).Trim();
			string argList = comma < 0 ? "" : content.Substring(comma + 1);

			if (!_bindings.TryGetValue(name, out MethodInfo method))
			{
				Debug.LogWarning($"[Dialogue] No binding named '{ name }'.");

				return;
			};

			ParameterInfo[] parameters = method.GetParameters();
			object[]        args       = DialogueArgs.Bind(parameters, DialogueArgs.Split(argList));

			method.Invoke(this, args);
		}

		#endregion
	};
};
