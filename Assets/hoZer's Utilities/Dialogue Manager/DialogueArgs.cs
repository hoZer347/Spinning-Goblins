using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;


namespace AdequateGames
{
	public static class DialogueArgs
	{
		public static List<string> Split(string raw)
		{
			var args = new List<string>();
			var sb   = new StringBuilder();
			bool inQuotes = false;

			for (int i = 0; i < raw.Length; i++)
			{
				char c = raw[i];

				if (c == '"')
				{ inQuotes = !inQuotes; sb.Append(c); }
				else if (c == ',' && !inQuotes)
				{ args.Add(sb.ToString().Trim()); sb.Clear(); }
				else
					sb.Append(c);
			};

			string last = sb.ToString().Trim();

			if (last.Length > 0 || args.Count > 0)
				args.Add(last);

			return args;
		}

		public static object[] Bind(ParameterInfo[] parameters, List<string> raw)
		{
			var args = new object[parameters.Length];

			for (int i = 0; i < parameters.Length; i++)
			{
				if (i < raw.Count)
					args[i] = Coerce(raw[i], parameters[i].ParameterType);
				else if (parameters[i].HasDefaultValue)
					args[i] = parameters[i].DefaultValue;
				else
					throw new ArgumentException($"missing argument '{parameters[i].Name}'");
			};

			return args;
		}

		static object Coerce(string token, Type type)
		{
			token = token.Trim();

			if (type == typeof(string)) return Unquote(token);
			if (type == typeof(int))    return int.Parse(token, CultureInfo.InvariantCulture);
			if (type == typeof(float))  return float.Parse(token.TrimEnd('f', 'F'), CultureInfo.InvariantCulture);
			if (type == typeof(double)) return double.Parse(token.TrimEnd('d', 'D'), CultureInfo.InvariantCulture);
			if (type == typeof(bool))   return bool.Parse(token);
			if (type.IsEnum)            return Enum.Parse(type, Unquote(token), ignoreCase: true);

			return Convert.ChangeType(Unquote(token), type, CultureInfo.InvariantCulture);
		}

		static string Unquote(string s)
			=> s.Length >= 2 && s[0] == '"' && s[^1] == '"'
				? s.Substring(1, s.Length - 2).Replace("\\\"", "\"")
				: s;
	};
};
