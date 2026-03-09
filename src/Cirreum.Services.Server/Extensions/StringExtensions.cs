namespace Cirreum;

using System.Text.RegularExpressions;

internal static partial class StringExtensions {

	[GeneratedRegex(@"(?<=[a-z\d])([A-Z])|(?<=[A-Z])([A-Z](?=[a-z]))")]
	private static partial Regex KebaberizeRegex();

	/// <summary>
	/// Converts a PascalCase or camelCase string to kebab-case.
	/// </summary>
	/// <example>"StartupHealthCheck" → "startup-health-check"</example>
	public static string Kebaberize(this string input) {
		if (string.IsNullOrEmpty(input)) {
			return input;
		}

		return KebaberizeRegex().Replace(input, "-$0").ToLowerInvariant();
	}

}
