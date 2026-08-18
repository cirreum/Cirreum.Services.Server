namespace Cirreum.Invocation.WebSockets;

using Cirreum.Authentication;
using Cirreum.Invocation;
using Cirreum.Invocation.Connections;
using System.Security.Claims;

/// <summary>
/// <see cref="IInvocationContext"/> for WebSocket-sourced invocations. Captures the
/// per-message snapshot of the connection's effective principal and authentication state,
/// together with the per-invocation DI scope, cancellation token, and parent
/// <see cref="IInvocationConnection"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Items"/> is a fresh per-invocation dictionary, distinct from the
/// per-connection <see cref="IInvocationConnection.Items"/>. Consumers that need state
/// to outlive a single WebSocket message should use <c>Connection.Items</c>.
/// </para>
/// <para>
/// At construction, the framework snapshots the connection-scoped authentication slots
/// (<c>AuthenticatedScheme</c>, <c>OriginScheme</c>, and
/// <c>ApplicationUserCache</c>) into <see cref="Items"/>. This gives consumers such as
/// <c>UserStateAccessor</c> a uniform per-invocation read surface across HTTP and
/// WebSocket sources without re-resolving the application user on every message.
/// </para>
/// <para>
/// The snapshot is isolated from the connection: writes to <see cref="Items"/> do not
/// propagate back to <c>Connection.Items</c>. Two-Phase Auth promotion updates the
/// connection-scoped authentication state, so the promoted principal and its origin
/// become visible when the next invocation snapshot is created.
/// </para>
/// <para>
/// The connection-scoped authentication state is established initially by
/// <see cref="WebSocketOrchestrator.HandleWebSocketAsync"/> and may subsequently be
/// updated by framework authentication behavior such as Two-Phase Auth promotion.
/// </para>
/// <para>
/// This context is used both for normal WebSocket message invocations and for synthetic
/// invocation scopes around connection lifecycle hooks
/// (<c>OnConnectedAsync</c> / <c>OnDisconnectedAsync</c>), allowing ambient consumers
/// such as <c>IUserStateAccessor</c> to operate normally inside
/// <see cref="IConnectionLifecycle"/> callbacks.
/// </para>
/// <para>
/// During disconnect, the framework constructs the context with an explicit
/// cleanup-budget token so <see cref="Aborted"/> represents the bounded cleanup window
/// rather than the connection's already-canceled token. Ambient consumers therefore
/// observe the same cancellation budget supplied to the disconnect lifecycle callback.
/// </para>
/// </remarks>
internal sealed class WebSocketInvocationContext : IInvocationContext {

	/// <summary>
	/// Standard constructor — <see cref="Aborted"/> tracks the connection's cancellation.
	/// Used for in-flight messages and the connect synthetic scope.
	/// </summary>
	internal WebSocketInvocationContext(
		WebSocketConnection connection,
		IServiceProvider services)
		: this(connection, services, connection.Aborted) {
	}

	/// <summary>
	/// Disconnect-path constructor — <see cref="Aborted"/> reflects the explicit cleanup
	/// budget rather than the connection's (already-canceled) token. The framework uses
	/// this overload during the disconnect synthetic scope so ambient consumers get the
	/// same bounded cleanup window the handler's <c>OnDisconnectedAsync(DisconnectInfo, CancellationToken)</c>
	/// parameter receives.
	/// </summary>
	internal WebSocketInvocationContext(
		WebSocketConnection connection,
		IServiceProvider services,
		CancellationToken aborted) {

		// Effective principal: a connection promoted mid-flight via Two-Phase Auth
		// flows the promoted identity into every subsequent invocation's snapshot;
		// un-promoted connections flow the upgrade-time principal.
		this.User = connection.EffectiveUser;
		this.Services = services;
		this.Aborted = aborted;
		this.Connection = connection;
		this.Items = SeedAuthSlots(connection);
	}

	public ClaimsPrincipal User { get; }

	public IDictionary<object, object?> Items { get; }

	public IServiceProvider Services { get; }

	public CancellationToken Aborted { get; }

	public string InvocationSource => InvocationSources.WebSocket;

	public IInvocationConnection? Connection { get; }

	private static Dictionary<object, object?> SeedAuthSlots(WebSocketConnection connection) {

		// Each invocation gets a fresh Items dictionary seeded from the connection-scoped
		// authentication state. This gives invocation consumers a uniform read surface
		// without requiring knowledge of Connection.Items, while preserving per-invocation
		// isolation: writes to invocation.Items never flow back to the connection.
		//
		// Two-Phase Auth promotion updates the connection's OriginScheme and evicts
		// ApplicationUserCache when it stamps the promoted principal. The next invocation
		// therefore snapshots the promoted subject's origin scheme without inheriting the
		// previous subject's cached application user; the normal lazy-resolution path
		// repopulates that cache for the promoted identity.
		//
		// AuthenticatedScheme intentionally survives promotion. It describes how the
		// connection was authenticated at establishment, while OriginScheme describes how
		// the current effective subject was established when that differs.

		var dict = new Dictionary<object, object?>();

		if (connection.Items.TryGetValue(
			AuthenticationContextKeys.AuthenticatedScheme,
			out var scheme)) {

			dict[AuthenticationContextKeys.AuthenticatedScheme] = scheme;
		}

		if (connection.Items.TryGetValue(
			AuthenticationContextKeys.OriginScheme,
			out var originScheme)) {

			dict[AuthenticationContextKeys.OriginScheme] = originScheme;
		}

		if (connection.Items.TryGetValue(
			AuthenticationContextKeys.ApplicationUserCache,
			out var appUser)) {

			dict[AuthenticationContextKeys.ApplicationUserCache] = appUser;
		}

		return dict;

	}

}
