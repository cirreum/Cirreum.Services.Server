namespace Cirreum.Http.Invocation;

using Cirreum.Invocation;
using Cirreum.Invocation.Connections;
using Cirreum.RemoteServices;
using Cirreum.Security;
using System.Security.Claims;

/// <summary>
/// Default <see cref="IInvocationContext"/> for HTTP-sourced invocations. Snapshots the
/// authenticated principal, aliases <c>HttpContext.Items</c> directly (same dictionary
/// reference — no copy), and exposes the request's DI scope and cancellation token.
/// </summary>
/// <remarks>
/// Items aliasing is intentional: existing framework code that reads/writes
/// <c>HttpContext.Items</c> through <see cref="AuthenticationContextKeys"/> slots
/// (e.g., the role-claims transformer, <c>UserStateAccessor</c>) continues to work
/// transparently when migrated to <see cref="IInvocationContext.Items"/>.
/// </remarks>
internal sealed class HttpInvocationContext(
	HttpContext http
) : IInvocationContext {

	public ClaimsPrincipal User => http.User;

	public IDictionary<object, object?> Items => http.Items;

	public IServiceProvider Services => http.RequestServices;

	public CancellationToken Aborted => http.RequestAborted;

	public string InvocationSource { get; } = InvocationSources.Http;

	public IInvocationConnection? Connection { get; } = null;


	/// <summary>
	/// App-name header value (<see cref="RemoteIdentityConstants.AppNameHeader"/>) snapshotted
	/// at middleware entry. Internal seam consumed by <c>UserStateAccessor</c> via feature-check cast,
	/// avoiding a re-read against <see cref="IHttpContextAccessor"/> mid-pipeline.
	/// </summary>
	public string? AppName => http.Request.Headers[RemoteIdentityConstants.AppNameHeader];

}