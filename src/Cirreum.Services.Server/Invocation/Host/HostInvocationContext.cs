namespace Cirreum.Invocation.Host;

using Cirreum.Invocation.Connections;
using System.Security.Claims;

/// <summary>
/// <see cref="IInvocationContext"/> for work the host starts on its own behalf. Owns a
/// fresh <see cref="Items"/> dictionary, a root-derived service scope, and a cancellation
/// token linked to application shutdown. Never belongs to a connection.
/// </summary>
internal sealed class HostInvocationContext(
	ClaimsPrincipal user,
	IDictionary<object, object?> items,
	IServiceProvider services,
	CancellationToken aborted,
	string invocationSource,
	string appName
) : IInvocationContext, IInvocationAppName {

	public ClaimsPrincipal User { get; } = user;

	public IDictionary<object, object?> Items { get; } = items;

	public IServiceProvider Services { get; } = services;

	public CancellationToken Aborted { get; } = aborted;

	public string InvocationSource { get; } = invocationSource;

	public IInvocationConnection? Connection => null;

	public string? AppName { get; } = appName;

}
