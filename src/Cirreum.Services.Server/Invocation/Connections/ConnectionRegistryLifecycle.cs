namespace Cirreum.Invocation.Connections;

/// <summary>
/// Framework-shipped <see cref="IConnectionLifecycle"/> that feeds
/// <see cref="IInvocationConnectionRegistry"/> from the connect/disconnect hooks the
/// source adapters (<c>InvocationContextHubFilter</c>, <c>WebSocketOrchestrator</c>)
/// already dispatch. Never rejects a connection — registration is bookkeeping, not
/// admission control.
/// </summary>
internal sealed class ConnectionRegistryLifecycle(
	IInvocationConnectionRegistry registry
) : IConnectionLifecycle {

	/// <inheritdoc />
	public ValueTask<bool> OnConnectedAsync(IInvocationConnection connection, CancellationToken cancellationToken) {
		registry.Register(connection);

		// Cleanup is tied to the connection's OWN lifetime token, not only the disconnect hook.
		// The raw-WebSocket orchestrator skips OnDisconnectedAsync when a connect is rejected (a
		// later IConnectionLifecycle returns false) or faults (a hook or the handler throws) — but
		// it still disposes the connection on those paths, and WebSocketConnection.DisposeAsync
		// cancels Aborted before disposing. Hooking Unregister to Aborted therefore guarantees
		// release on every teardown path for both transports (SignalR's ConnectionAborted cancels
		// on abort/disconnect the same way), closing the "registered but never unregistered" leak.
		// OnDisconnectedAsync below still removes promptly on the graceful path; Unregister is
		// idempotent, so the two signals never double-remove wrongly. Registering on an
		// already-canceled token runs the callback inline — a connection aborted mid-handshake is
		// registered then immediately removed, which is the correct end state.
		connection.Aborted.Register(
			static state => {
				var (reg, id) = ((IInvocationConnectionRegistry Registry, string ConnectionId))state!;
				reg.Unregister(id);
			},
			(registry, connection.ConnectionId));

		return ValueTask.FromResult(true);
	}

	/// <inheritdoc />
	public ValueTask OnDisconnectedAsync(IInvocationConnection connection, DisconnectInfo info, CancellationToken cancellationToken) {
		registry.Unregister(connection.ConnectionId);
		return ValueTask.CompletedTask;
	}

}
