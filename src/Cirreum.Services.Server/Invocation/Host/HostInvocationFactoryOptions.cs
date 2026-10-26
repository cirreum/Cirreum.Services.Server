namespace Cirreum.Invocation.Host;

/// <summary>
/// Composition-time settings for host invocations. Bound from the
/// <c>Cirreum:Invocation:Host</c> configuration section.
/// </summary>
public sealed class HostInvocationFactoryOptions {

	/// <summary>
	/// The roles minted on a host invocation's principal when
	/// <see cref="HostInvocationOptions.Roles"/> is <see langword="null"/>. Empty by default.
	/// </summary>
	public IReadOnlyList<string> DefaultRoles { get; set; } = [];

}
