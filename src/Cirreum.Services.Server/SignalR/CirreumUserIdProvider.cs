namespace Cirreum.Invocation.SignalR;

using Cirreum.Security;
using Microsoft.AspNetCore.SignalR;

/// <summary>
/// <see cref="IUserIdProvider"/> that aligns SignalR's user-addressing
/// (<c>Clients.User(userId)</c>, <c>IHubContext.Clients.User(...)</c>) with the framework's
/// subject identity: <see cref="ClaimsHelper.ResolveId(System.Security.Claims.ClaimsPrincipal)"/>
/// — the same resolution behind <c>IUserState.Id</c>, auth-event <c>Subject</c> values, and the
/// connection registry's subject lookup. SignalR's own default uses
/// <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/> only, which diverges from the
/// framework's oid-preferring resolution on Entra-backed apps (pairwise <c>sub</c> vs tenant-wide
/// <c>oid</c>) — two user-addressing systems that silently disagree about who a connection belongs to.
/// </summary>
/// <remarks>
/// <para>
/// SignalR computes the user id <em>once at connection time</em> and caches it for the connection's
/// lifetime — an inherent SignalR design point, not something this provider can change. A connection
/// promoted mid-flight via Two-Phase Auth therefore remains addressed by its upgrade-time identity in
/// <c>Clients.User(...)</c>. For promotion-aware targeting, use
/// <c>IInvocationConnectionRegistry.FindBySubject</c> + <c>IInvocationConnection.SendAsync</c>, which
/// resolve identity at query time.
/// </para>
/// <para>
/// Registered by <c>TryAddSignalRInvocationFilter</c> only when SignalR's own
/// <see cref="DefaultUserIdProvider"/> is still in place — an app-registered custom
/// <see cref="IUserIdProvider"/> always wins.
/// </para>
/// </remarks>
internal sealed class CirreumUserIdProvider : IUserIdProvider {

	/// <inheritdoc />
	public string? GetUserId(HubConnectionContext connection) =>
		connection.User is { } user ? ClaimsHelper.ResolveId(user) : null;

}
