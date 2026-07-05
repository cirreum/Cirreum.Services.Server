namespace Microsoft.Extensions.DependencyInjection;

using Cirreum.Invocation.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Wires Cirreum's SignalR invocation support when — and only when — the app has opted into
/// SignalR via the native <c>services.AddSignalR()</c>. Cirreum ships no
/// <c>AddSignalR</c> of its own; it auto-detects the app's registration and contributes its
/// <see cref="InvocationContextHubFilter"/> transparently.
/// </summary>
public static class SignalRInvocationExtensions {

	/// <summary>
	/// If SignalR is registered in <paramref name="services"/>, adds the
	/// <see cref="InvocationContextHubFilter"/> (which publishes <c>IInvocationContext</c> per
	/// Hub method invocation and drives <c>IConnectionLifecycle</c> hooks) to the global
	/// <see cref="HubOptions"/> filter chain. No-op when SignalR is absent — apps that don't
	/// use SignalR pay nothing and pull no SignalR behavior.
	/// </summary>
	/// <param name="services">The service collection — inspected for the SignalR bootstrap marker.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Must be invoked at <c>Build()</c>-time (after the app has had a chance to call
	/// <c>AddSignalR()</c>), not during the spine bootstrap — <c>Cirreum.Runtime.Server</c>'s
	/// build step calls this. Detection keys on <see cref="IUserIdProvider"/>, which
	/// <c>AddSignalRCore</c> registers unconditionally.
	/// </remarks>
	public static IServiceCollection TryAddSignalRInvocationFilter(this IServiceCollection services) {

		ArgumentNullException.ThrowIfNull(services);

		// SignalR not wired by the app → contribute nothing.
		if (!services.Any(d => d.ServiceType == typeof(IUserIdProvider))) {
			return services;
		}

		// One filter, all hubs — AddFilter on the global HubOptions applies it to every Hub
		// registered in the host. Idempotent: TryAddSingleton dedupes the filter instance, and
		// re-adding the same filter type to HubOptions is harmless (the runtime is called once
		// per host build).
		services.TryAddSingleton<InvocationContextHubFilter>();
		services.Configure<HubOptions>(o => o.AddFilter<InvocationContextHubFilter>());

		// Align SignalR's Clients.User(...) addressing with the framework's subject identity
		// (ClaimsHelper.ResolveId ≡ IUserState.Id ≡ auth-event Subject) — but only while
		// SignalR's own default provider is still in place. An app-registered custom
		// IUserIdProvider (any other implementation type) always wins.
		var userIdProvider = services.LastOrDefault(d => d.ServiceType == typeof(IUserIdProvider));
		if (userIdProvider?.ImplementationType == typeof(DefaultUserIdProvider)) {
			services.Replace(ServiceDescriptor.Singleton<IUserIdProvider, CirreumUserIdProvider>());
		}

		return services;
	}

}
