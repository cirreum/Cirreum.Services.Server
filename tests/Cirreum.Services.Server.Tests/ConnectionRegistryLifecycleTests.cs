namespace Cirreum.Services.Server.Tests;

using Cirreum.Invocation.Connections;

public sealed class ConnectionRegistryLifecycleTests {

	[Fact]
	public async Task On_connected_registers_the_connection_and_accepts_it() {
		var registry = new InvocationConnectionRegistry();
		var lifecycle = new ConnectionRegistryLifecycle(registry);
		var connection = InvocationConnectionRegistryTests.Connection("c1", "alice");

		var accepted = await lifecycle.OnConnectedAsync(connection, CancellationToken.None);

		accepted.Should().BeTrue();
		registry.Find("c1").Should().BeSameAs(connection);
	}

	[Fact]
	public async Task On_disconnected_unregisters_the_connection() {
		var registry = new InvocationConnectionRegistry();
		var lifecycle = new ConnectionRegistryLifecycle(registry);
		var connection = InvocationConnectionRegistryTests.Connection("c1", "alice");
		registry.Register(connection);

		await lifecycle.OnDisconnectedAsync(
			connection,
			new DisconnectInfo(WasGraceful: true, Exception: null, Reason: null),
			CancellationToken.None);

		registry.Find("c1").Should().BeNull();
	}

	[Fact]
	public async Task Aborting_the_connection_unregisters_it_even_when_disconnect_is_skipped() {
		// The WebSocket connect-rejection / fault path disposes the connection (cancelling Aborted)
		// but never calls OnDisconnectedAsync. Cleanup must still happen via the Aborted hook — this
		// is the regression guard for the "registered but never unregistered" leak.
		var registry = new InvocationConnectionRegistry();
		var lifecycle = new ConnectionRegistryLifecycle(registry);
		using var cts = new CancellationTokenSource();
		var connection = InvocationConnectionRegistryTests.Connection("c1", "alice");
		connection.Aborted.Returns(cts.Token);

		await lifecycle.OnConnectedAsync(connection, cts.Token);
		registry.Find("c1").Should().BeSameAs(connection);

		cts.Cancel(); // simulates DisposeAsync cancelling Aborted with no OnDisconnectedAsync

		registry.Find("c1").Should().BeNull();
	}

	[Fact]
	public async Task Registering_when_already_aborted_leaves_the_connection_unregistered() {
		var registry = new InvocationConnectionRegistry();
		var lifecycle = new ConnectionRegistryLifecycle(registry);
		using var cts = new CancellationTokenSource();
		cts.Cancel(); // connection aborted before the hook runs
		var connection = InvocationConnectionRegistryTests.Connection("c1", "alice");
		connection.Aborted.Returns(cts.Token);

		await lifecycle.OnConnectedAsync(connection, cts.Token);

		// Register-then-inline-Unregister: a connection aborted mid-handshake must not linger.
		registry.Find("c1").Should().BeNull();
	}

}
