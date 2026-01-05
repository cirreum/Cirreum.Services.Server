namespace Cirreum.Extensions;

using FluentValidation.Results;

/// <summary>
/// Provides extension methods for FluentValidation types.
/// </summary>
public static class FluentValidationExtensions {
	extension(ValidationFailure failure) {
		/// <summary>
		/// Converts a FluentValidation <see cref="ValidationFailure"/> to a <see cref="FailureModel"/>.
		/// </summary>
		/// <returns>
		/// A new <see cref="FailureModel"/> instance containing the mapped validation failure details.
		/// </returns>
		/// <remarks>
		/// The <see cref="ValidationFailure.AttemptedValue"/> and <see cref="ValidationFailure.CustomState"/> 
		/// properties are converted to strings, defaulting to an empty string if null.
		/// </remarks>
		public FailureModel ToFailureModel() {
			return new FailureModel {
				PropertyName = failure.PropertyName,
				ErrorMessage = failure.ErrorMessage,
				ErrorCode = failure.ErrorCode,
				Severity = (FailureSeverity)(int)failure.Severity,
				AttemptedValue = failure.AttemptedValue?.ToString() ?? "",
				CustomState = failure.CustomState?.ToString() ?? ""
			};
		}
	}
}