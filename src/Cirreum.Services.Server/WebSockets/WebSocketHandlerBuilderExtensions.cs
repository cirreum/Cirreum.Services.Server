namespace Microsoft.Extensions.Hosting;

using Cirreum.Invocation.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// App-facing composition verb for Cirreum's WebSocket sub-framework.
/// WebSocket support is opt-in via a Cirreum extension (ASP.NET ships no native handler
/// abstraction — only the raw <c>WebSocket</c> primitive behind <c>UseWebSockets()</c>).
/// </summary>
public static class WebSocketHandlerBuilderExtensions {

	/// <summary>
	/// Registers the WebSocket orchestrator and the supplied <typeparamref name="THandler"/>
	/// so it can be mapped with <c>app.MapWebSocketHandler&lt;THandler&gt;(path)</c>. Call once
	/// per handler type; the orchestrator registration is idempotent.
	/// </summary>
	/// <typeparam name="THandler">The application's <see cref="WebSocketHandler"/> implementation.
	/// Resolved once per connection from a connection-scoped DI scope.</typeparam>
	/// <param name="builder">The host application builder.</param>
	/// <returns>The builder for chaining.</returns>
	public static IHostApplicationBuilder AddWebSocketHandler<THandler>(this IHostApplicationBuilder builder)
		where THandler : WebSocketHandler {

		ArgumentNullException.ThrowIfNull(builder);

		// The orchestrator is the per-connection driver — one process-wide singleton drives
		// every handler. Idempotent across multiple AddWebSocketHandler<T> calls.
		builder.Services.TryAddSingleton<WebSocketOrchestrator>();

		// One handler instance per connection: the orchestrator resolves THandler from the
		// per-connection scope it creates, so scoped lifetime gives one instance per connection.
		builder.Services.TryAddScoped<THandler>();

		return builder;
	}

}
