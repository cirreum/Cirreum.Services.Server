namespace Cirreum.Invocation;

/// <summary>
/// Default <see cref="AsyncLocal{T}"/>-backed implementation of
/// <see cref="IInvocationContextAccessor"/>. Registered as a singleton; safe for
/// concurrent use across async flows.
/// </summary>
public sealed class InvocationContextAccessor : IInvocationContextAccessor {

	private static readonly AsyncLocal<IInvocationContext?> _current = new();

	/// <inheritdoc />
	public IInvocationContext? Current => _current.Value;

	/// <inheritdoc />
	public void Set(IInvocationContext invocation) => _current.Value = invocation;

	/// <inheritdoc />
	public void Clear() => _current.Value = null;

}
