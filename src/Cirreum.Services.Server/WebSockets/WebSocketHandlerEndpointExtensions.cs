namespace Microsoft.AspNetCore.Builder;

using Cirreum.Invocation.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Endpoint-mapping extension for Cirreum's WebSocket sub-framework. This is
/// a Cirreum-named extension (not <c>MapWebSocket</c>) because ASP.NET ships no native
/// handler-style endpoint for WebSockets. It returns <see cref="IEndpointConventionBuilder"/>
/// so apps chain <c>.RequireAuthorization(...)</c> / <c>.WithName(...)</c> exactly as with
/// <c>MapHub</c> / <c>MapGet</c>.
/// </summary>
public static class WebSocketHandlerEndpointExtensions {

	/// <summary>
	/// Maps a WebSocket endpoint at <paramref name="path"/> driven by
	/// <typeparamref name="THandler"/>. Register the handler first with
	/// <c>builder.AddWebSocketHandler&lt;THandler&gt;()</c>.
	/// </summary>
	/// <typeparam name="THandler">The handler type registered via <c>AddWebSocketHandler&lt;THandler&gt;()</c>.</typeparam>
	/// <param name="endpoints">The endpoint route builder.</param>
	/// <param name="path">The request path to map (for example, <c>"/chat"</c>).</param>
	/// <param name="configure">Optional per-handler tuning (message/buffer limits, disconnect
	/// budget, keep-alive). Apps that want config-driven limits bind their own
	/// <c>IConfiguration</c> inside this callback.</param>
	/// <returns>An <see cref="IEndpointConventionBuilder"/> for further endpoint conventions.</returns>
	public static IEndpointConventionBuilder MapWebSocketHandler<THandler>(
		this IEndpointRouteBuilder endpoints,
		string path,
		Action<WebSocketHandlerOptions>? configure = null)
		where THandler : WebSocketHandler {

		ArgumentNullException.ThrowIfNull(endpoints);
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		var options = new WebSocketHandlerOptions();
		configure?.Invoke(options);
		options.Validate();

		var orchestrator = endpoints.ServiceProvider.GetRequiredService<WebSocketOrchestrator>();

		// Use Map() — not MapGet() — because a WebSocket upgrade arrives as GET (HTTP/1.1,
		// RFC 6455) OR CONNECT (HTTP/2+, RFC 8441/9220). Restricting to GET would 405 HTTP/2+
		// clients. Excluded from OpenAPI/Swagger — WebSocket isn't a REST operation.
		return endpoints
			.Map(path, async (HttpContext context) =>
				await orchestrator.HandleWebSocketAsync(
					context, typeof(THandler), options, context.RequestAborted))
			.WithMetadata(new ExcludeFromDescriptionAttribute());
	}

}
