namespace Cirreum.Services.Server.Tests;

using Cirreum.Authentication.Events;
using Cirreum.Invocation.Connections;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class ConnectionTerminationHandlerTests {

	private static readonly DateTimeOffset Now = new(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);

	private static (ConnectionTerminationHandler handler, InvocationConnectionRegistry registry) Build() {
		var registry = new InvocationConnectionRegistry();
		var handler = new ConnectionTerminationHandler(registry, NullLogger<ConnectionTerminationHandler>.Instance);
		return (handler, registry);
	}

	[Fact]
	public async Task Credential_revoked_aborts_all_of_the_subjects_connections_and_no_others() {
		var (handler, registry) = Build();
		var aliceTab = InvocationConnectionRegistryTests.Connection("c1", "alice");
		var alicePhone = InvocationConnectionRegistryTests.Connection("c2", "alice");
		var bob = InvocationConnectionRegistryTests.Connection("c3", "bob");
		registry.Register(aliceTab);
		registry.Register(alicePhone);
		registry.Register(bob);

		await handler.HandleAsync(new CredentialRevoked("cred-1", "alice", Now));

		aliceTab.Received(1).Abort();
		alicePhone.Received(1).Abort();
		bob.DidNotReceive().Abort();
	}

	[Fact]
	public async Task User_account_disabled_aborts_all_of_the_subjects_connections() {
		var (handler, registry) = Build();
		var alice = InvocationConnectionRegistryTests.Connection("c1", "alice");
		var bob = InvocationConnectionRegistryTests.Connection("c2", "bob");
		registry.Register(alice);
		registry.Register(bob);

		await handler.HandleAsync(new UserAccountDisabled("alice", Now));

		alice.Received(1).Abort();
		bob.DidNotReceive().Abort();
	}

	[Fact]
	public async Task Session_termination_without_a_session_id_aborts_all_of_the_subjects_connections() {
		var (handler, registry) = Build();
		var aliceTab = InvocationConnectionRegistryTests.Connection("c1", "alice");
		var alicePhone = InvocationConnectionRegistryTests.Connection("c2", "alice");
		registry.Register(aliceTab);
		registry.Register(alicePhone);

		await handler.HandleAsync(new SessionTerminationRequested("alice", Now));

		aliceTab.Received(1).Abort();
		alicePhone.Received(1).Abort();
	}

	[Fact]
	public async Task Session_termination_scoped_to_a_connection_id_aborts_only_that_connection() {
		var (handler, registry) = Build();
		var target = InvocationConnectionRegistryTests.Connection("c1", "alice");
		var other = InvocationConnectionRegistryTests.Connection("c2", "alice");
		registry.Register(target);
		registry.Register(other);

		await handler.HandleAsync(new SessionTerminationRequested("alice", Now) { SessionId = "c1" });

		target.Received(1).Abort();
		other.DidNotReceive().Abort();
	}

	[Fact]
	public async Task Session_termination_scoped_to_a_sid_claim_aborts_the_whole_browser_session_only() {
		var (handler, registry) = Build();
		var tabOne = InvocationConnectionRegistryTests.Connection("c1", "alice", sid: "browser-1");
		var tabTwo = InvocationConnectionRegistryTests.Connection("c2", "alice", sid: "browser-1");
		var phone = InvocationConnectionRegistryTests.Connection("c3", "alice", sid: "phone-1");
		registry.Register(tabOne);
		registry.Register(tabTwo);
		registry.Register(phone);

		await handler.HandleAsync(new SessionTerminationRequested("alice", Now) { SessionId = "browser-1" });

		tabOne.Received(1).Abort();
		tabTwo.Received(1).Abort();
		phone.DidNotReceive().Abort();
	}

	[Fact]
	public async Task Session_scoping_never_widens_beyond_the_subject() {
		var (handler, registry) = Build();
		// Bob's connection shares the same sid value — but the event names alice.
		var bob = InvocationConnectionRegistryTests.Connection("c1", "bob", sid: "shared-sid");
		registry.Register(bob);

		await handler.HandleAsync(new SessionTerminationRequested("alice", Now) { SessionId = "shared-sid" });

		bob.DidNotReceive().Abort();
	}

	[Fact]
	public async Task A_scoped_request_with_no_match_terminates_nothing() {
		var (handler, registry) = Build();
		var alice = InvocationConnectionRegistryTests.Connection("c1", "alice");
		registry.Register(alice);

		await handler.HandleAsync(new SessionTerminationRequested("alice", Now) { SessionId = "unknown-session" });

		alice.DidNotReceive().Abort();
	}

	[Fact]
	public async Task A_promoted_connection_is_terminated_under_its_promoted_subject() {
		var (handler, registry) = Build();
		var connection = InvocationConnectionRegistryTests.Connection("c1", subject: null); // pending-auth upgrade
		registry.Register(connection);
		InvocationConnectionRegistryTests.Promote(connection, "alice");

		await handler.HandleAsync(new CredentialRevoked("cred-1", "alice", Now));

		connection.Received(1).Abort();
	}

	[Fact]
	public async Task Null_events_are_rejected() {
		var (handler, _) = Build();

		await ((Func<Task>)(async () => await handler.HandleAsync((CredentialRevoked)null!))).Should().ThrowAsync<ArgumentNullException>();
		await ((Func<Task>)(async () => await handler.HandleAsync((UserAccountDisabled)null!))).Should().ThrowAsync<ArgumentNullException>();
		await ((Func<Task>)(async () => await handler.HandleAsync((SessionTerminationRequested)null!))).Should().ThrowAsync<ArgumentNullException>();
	}

}
