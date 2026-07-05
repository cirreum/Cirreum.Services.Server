namespace Cirreum.Services.Server.Tests;

using Cirreum.Authentication;
using Cirreum.Invocation.Connections;
using System.Security.Claims;

public sealed class InvocationConnectionRegistryTests {

	internal static IInvocationConnection Connection(string connectionId, string? subject = null, string? sid = null) {
		var connection = Substitute.For<IInvocationConnection>();
		connection.ConnectionId.Returns(connectionId);
		connection.Items.Returns(new Dictionary<object, object?>());
		connection.InvocationSource.Returns("test");

		var claims = new List<Claim>();
		if (subject is not null) {
			claims.Add(new Claim("sub", subject));
		}
		if (sid is not null) {
			claims.Add(new Claim("sid", sid));
		}
		var identity = subject is null ? new ClaimsIdentity() : new ClaimsIdentity(claims, "test");
		connection.User.Returns(new ClaimsPrincipal(identity));
		// No EffectiveUser setup needed: it's an extension member (Contracts 1.4.0), so the
		// real logic executes against the substitute's stubbed Items/User.
		return connection;
	}

	internal static void Promote(IInvocationConnection connection, string subject) {
		var identity = new ClaimsIdentity([new Claim("sub", subject)], "promoted");
		connection.Items[AuthenticationContextKeys.PromotedPrincipal] = new ClaimsPrincipal(identity);
	}

	[Fact]
	public void Register_then_find_returns_the_connection() {
		var registry = new InvocationConnectionRegistry();
		var connection = Connection("c1", "alice");

		registry.Register(connection);

		registry.Find("c1").Should().BeSameAs(connection);
	}

	[Fact]
	public void Register_is_idempotent_by_overwrite_on_connection_id() {
		var registry = new InvocationConnectionRegistry();
		var first = Connection("c1", "alice");
		var second = Connection("c1", "alice");

		registry.Register(first);
		registry.Register(second);

		registry.All().Should().ContainSingle().Which.Should().BeSameAs(second);
	}

	[Fact]
	public void Unregister_removes_the_connection_and_unknown_ids_are_no_ops() {
		var registry = new InvocationConnectionRegistry();
		registry.Register(Connection("c1", "alice"));

		registry.Unregister("c1");
		registry.Unregister("never-registered");

		registry.Find("c1").Should().BeNull();
		registry.All().Should().BeEmpty();
	}

	[Fact]
	public void FindBySubject_returns_only_the_subjects_connections() {
		var registry = new InvocationConnectionRegistry();
		var aliceTab = Connection("c1", "alice");
		var alicePhone = Connection("c2", "alice");
		var bob = Connection("c3", "bob");
		registry.Register(aliceTab);
		registry.Register(alicePhone);
		registry.Register(bob);

		var matches = registry.FindBySubject("alice").ToList();

		matches.Should().BeEquivalentTo([aliceTab, alicePhone]);
	}

	[Fact]
	public void FindBySubject_with_no_matches_is_empty() {
		var registry = new InvocationConnectionRegistry();
		registry.Register(Connection("c1", "alice"));

		registry.FindBySubject("carol").Should().BeEmpty();
	}

	[Fact]
	public void A_connection_with_no_resolvable_subject_matches_no_subject() {
		var registry = new InvocationConnectionRegistry();
		registry.Register(Connection("c1", subject: null)); // anonymous / pending-auth

		registry.FindBySubject("alice").Should().BeEmpty();
		registry.All().Should().ContainSingle(); // still tracked by id
	}

	[Fact]
	public void A_promoted_connection_is_found_under_the_promoted_subject_not_the_original() {
		var registry = new InvocationConnectionRegistry();
		var connection = Connection("c1", subject: null); // anonymous-pending-auth upgrade
		registry.Register(connection);
		registry.FindBySubject("alice").Should().BeEmpty();

		// Two-Phase Auth promotion mid-connection: no re-registration required — the
		// registry resolves subjects at query time from the effective principal.
		Promote(connection, "alice");

		registry.FindBySubject("alice").Should().ContainSingle().Which.Should().BeSameAs(connection);
	}

	[Fact]
	public void Re_promotion_moves_the_connection_to_the_new_subject() {
		var registry = new InvocationConnectionRegistry();
		var connection = Connection("c1", "alice");
		registry.Register(connection);

		Promote(connection, "alice-verified");

		registry.FindBySubject("alice").Should().BeEmpty();
		registry.FindBySubject("alice-verified").Should().ContainSingle();
	}

	[Fact]
	public void Register_with_a_null_connection_throws() {
		var act = () => new InvocationConnectionRegistry().Register(null!);

		act.Should().Throw<ArgumentNullException>();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Blank_arguments_are_rejected(string? value) {
		var registry = new InvocationConnectionRegistry();

		((Action)(() => registry.Unregister(value!))).Should().Throw<ArgumentException>();
		((Action)(() => registry.FindBySubject(value!))).Should().Throw<ArgumentException>();
		((Action)(() => registry.Find(value!))).Should().Throw<ArgumentException>();
	}

}
