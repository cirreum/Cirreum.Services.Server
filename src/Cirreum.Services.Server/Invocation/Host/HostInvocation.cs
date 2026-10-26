namespace Cirreum.Invocation.Host;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The handle for one host invocation. Disposal clears the ambient accessor, releases the
/// linked cancellation source, and disposes the invocation's service scope.
/// </summary>
internal sealed class HostInvocation(
	IInvocationContext context,
	IInvocationContextAccessor accessor,
	AsyncServiceScope scope,
	CancellationTokenSource aborted
) : IHostInvocation {

	private bool _disposed;

	public IInvocationContext Context { get; } = context;

	// Deliberately not an async method: an async method runs on a copy of the caller's
	// ExecutionContext, so an AsyncLocal cleared inside it would still read as set on the
	// caller's flow after the await. Clearing synchronously here reaches the flow that
	// called Begin.
	public ValueTask DisposeAsync() {
		if (this._disposed) {
			return default;
		}
		this._disposed = true;

		accessor.Clear();
		aborted.Dispose();
		return scope.DisposeAsync();
	}

}
