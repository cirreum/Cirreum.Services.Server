namespace Cirreum.Services.Server.Tests;

using Cirreum.Authentication.Events;
using Cirreum.Invocation.Connections;
using Microsoft.Extensions.DependencyInjection;

public sealed class PhaseBRegistrationTests {

	private static ServiceProvider BuildProvider() {
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddCoreServices();
		return services.BuildServiceProvider();
	}

	[Fact]
	public void AddCoreServices_registers_the_connection_registry_as_a_singleton() {
		using var provider = BuildProvider();

		var registry = provider.GetRequiredService<IInvocationConnectionRegistry>();

		registry.Should().BeOfType<InvocationConnectionRegistry>();
		provider.GetRequiredService<IInvocationConnectionRegistry>().Should().BeSameAs(registry);
	}

	[Fact]
	public void AddCoreServices_registers_the_registry_feeding_lifecycle() {
		using var provider = BuildProvider();

		provider.GetServices<IConnectionLifecycle>()
			.Should().ContainSingle(l => l is ConnectionRegistryLifecycle);
	}

	[Fact]
	public void AddCoreServices_registers_the_terminator_for_all_three_events() {
		using var provider = BuildProvider();

		provider.GetServices<IAuthenticationEventHandler<CredentialRevoked>>()
			.Should().ContainSingle(h => h is ConnectionTerminationHandler);
		provider.GetServices<IAuthenticationEventHandler<UserAccountDisabled>>()
			.Should().ContainSingle(h => h is ConnectionTerminationHandler);
		provider.GetServices<IAuthenticationEventHandler<SessionTerminationRequested>>()
			.Should().ContainSingle(h => h is ConnectionTerminationHandler);
	}

	[Fact]
	public void AddCoreServices_is_idempotent_for_the_phase_b_registrations() {
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddCoreServices();
		services.AddCoreServices();
		using var provider = services.BuildServiceProvider();

		provider.GetServices<IConnectionLifecycle>()
			.Where(l => l is ConnectionRegistryLifecycle).Should().ContainSingle();
		provider.GetServices<IAuthenticationEventHandler<CredentialRevoked>>()
			.Where(h => h is ConnectionTerminationHandler).Should().ContainSingle();
	}

}
