namespace Cirreum.Services.Server.Tests;

using Cirreum.Invocation.SignalR;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

public sealed class CirreumUserIdProviderTests {

	[Fact]
	public void Wiring_replaces_signalrs_default_user_id_provider() {
		var services = new ServiceCollection();
		services.AddSignalR();

		services.TryAddSignalRInvocationFilter();

		services.Last(d => d.ServiceType == typeof(IUserIdProvider))
			.ImplementationType.Should().Be(typeof(CirreumUserIdProvider));
	}

	[Fact]
	public void Wiring_never_stomps_an_app_registered_custom_provider() {
		var services = new ServiceCollection();
		services.AddSignalR();
		services.Replace(ServiceDescriptor.Singleton<IUserIdProvider, AppCustomUserIdProvider>());

		services.TryAddSignalRInvocationFilter();

		services.Last(d => d.ServiceType == typeof(IUserIdProvider))
			.ImplementationType.Should().Be(typeof(AppCustomUserIdProvider));
	}

	[Fact]
	public void Wiring_contributes_nothing_when_signalr_is_absent() {
		var services = new ServiceCollection();

		services.TryAddSignalRInvocationFilter();

		services.Should().NotContain(d => d.ServiceType == typeof(IUserIdProvider));
	}

	[Fact]
	public void User_id_resolves_via_the_framework_helper_not_name_identifier_alone() {
		// oid must win over NameIdentifier — the whole point vs SignalR's default.
		var identity = new ClaimsIdentity(
			[new Claim("oid", "tenant-wide-object-id"), new Claim(ClaimTypes.NameIdentifier, "pairwise-sub")],
			"test");
		var hubConnection = Substitute.For<HubConnectionContext>(
			Substitute.For<ConnectionContext>(),
			new HubConnectionContextOptions(),
			NullLoggerFactory.Instance);
		hubConnection.User.Returns(new ClaimsPrincipal(identity));

		new CirreumUserIdProvider().GetUserId(hubConnection).Should().Be("tenant-wide-object-id");
	}

	private sealed class AppCustomUserIdProvider : IUserIdProvider {
		public string? GetUserId(HubConnectionContext connection) => "app-decided";
	}

}
