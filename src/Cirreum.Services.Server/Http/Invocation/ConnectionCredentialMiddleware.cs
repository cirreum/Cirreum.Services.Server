namespace Cirreum.Http.Invocation;

using Cirreum.Invocation.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Net.Http.Headers;

/// <summary>
/// Promotes a query-carried bearer credential into the <c>Authorization</c> header on
/// endpoints whose invocations arrive over a long-lived connection.
/// </summary>
/// <remarks>
/// <para>
/// A browser cannot set request headers on a WebSocket upgrade, so a client connecting over
/// WebSockets sends its credential as an <c>access_token</c> query parameter — the convention
/// SignalR's own clients follow, and the only one available to them. Promoting it here means
/// every authentication scheme, and every scheme selector, reads the credential from the one
/// place it has always read it, and none of them need to know this case exists.
/// </para>
/// <para>
/// The promotion applies only where a client has no alternative: the request must target an
/// endpoint whose invocations arrive over a connection, and must not already carry an
/// <c>Authorization</c> header. A query parameter on any other endpoint is left alone and
/// carries no authority.
/// </para>
/// <para>
/// The value is promoted verbatim, so a scheme prefix carried inside the credential — part of
/// the opaque secret its issuer minted and stored — continues to route dispatch.
/// </para>
/// <para>
/// Register between <c>UseRouting</c> and <c>UseAuthentication</c>: the endpoint must be
/// resolved for the scoping test, and the header must be in place before any scheme reads it.
/// </para>
/// <para>
/// The query-parameter name and the endpoint test are duplicated here rather than shared with
/// the authentication track: this package deliberately references no authentication-track
/// package, the same trade the invocation adapters make with their local context-key constants.
/// </para>
/// </remarks>
internal sealed class ConnectionCredentialMiddleware(RequestDelegate next) {

	private const string AccessTokenQueryParameter = "access_token";

	public async Task InvokeAsync(HttpContext context) {

		if (ShouldPromote(context)) {

			var credential = context.Request.Query[AccessTokenQueryParameter].ToString();
			if (!string.IsNullOrEmpty(credential)) {
				context.Request.Headers.Authorization = $"Bearer {credential}";
			}
		}

		await next(context);

	}

	private static bool ShouldPromote(HttpContext context) {

		// An Authorization header the client could send is authoritative, whatever scheme it
		// names: promoting over it would override a deliberate choice.
		if (context.Request.Headers.ContainsKey(HeaderNames.Authorization)) {
			return false;
		}

		// A SignalR hub carries SignalR's own metadata; every other connection endpoint the
		// framework maps carries InvocationConnectionMetadata.
		var metadata = context.GetEndpoint()?.Metadata;
		if (metadata is null) {
			return false;
		}

		return metadata.GetMetadata<HubMetadata>() is not null
			|| metadata.GetMetadata<InvocationConnectionMetadata>() is not null;
	}

}
