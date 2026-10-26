namespace Cirreum.Invocation;

/// <summary>
/// An invocation context that knows the application name to attribute its subject to.
/// Read by <c>UserStateAccessor</c> through a feature-check cast.
/// </summary>
internal interface IInvocationAppName {

	/// <summary>
	/// The application name for the invocation's subject, or <see langword="null"/> when none
	/// was supplied.
	/// </summary>
	string? AppName { get; }

}
