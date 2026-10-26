namespace Cirreum.Invocation.Host;

using Cirreum.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Claims;

/// <summary>
/// Default <see cref="IHostInvocationFactory"/>. Registered as a singleton.
/// </summary>
internal sealed class HostInvocationFactory(
	IInvocationContextAccessor accessor,
	IServiceScopeFactory scopeFactory,
	IHostApplicationLifetime lifetime,
	IHostEnvironment environment,
	IOptions<HostInvocationFactoryOptions> options
) : IHostInvocationFactory {

	internal const string NestedMessage =
		"An invocation is already current on this async flow; a host invocation never runs inside " +
		"another invocation. Detach the work with ExecutionContext.SuppressFlow() before queuing it, " +
		"or call IHostInvocationFactory.StartDetached.";

	internal const string UnauthenticatedUserMessage =
		"HostInvocationOptions.User must be an authenticated principal. Leave it null to run as the host.";

	public IHostInvocation Begin(HostInvocationOptions? options = null) {

		if (accessor.Current is not null) {
			throw new InvalidOperationException(NestedMessage);
		}

		options ??= new HostInvocationOptions();

		var appName = environment.ApplicationName;
		var roles = options.Roles ?? this.Options.DefaultRoles;
		var user = options.User is null
			? CreateHostPrincipal(appName, roles)
			: CreateContinuationPrincipal(options.User, roles);

		var items = new Dictionary<object, object?> {
			[AuthenticationContextKeys.AuthenticatedScheme] = HostInvocationDefaults.Scheme
		};
		if (options.User is not null && !string.IsNullOrWhiteSpace(options.OriginScheme)) {
			items[AuthenticationContextKeys.OriginScheme] = options.OriginScheme;
		}

		var aborted = CancellationTokenSource.CreateLinkedTokenSource(
			lifetime.ApplicationStopping,
			options.CancellationToken);
		var scope = scopeFactory.CreateAsyncScope();

		var context = new HostInvocationContext(
			user,
			items,
			scope.ServiceProvider,
			aborted.Token,
			options.Source,
			appName);

		accessor.Set(context);
		return new HostInvocation(context, accessor, scope, aborted);

	}

	private HostInvocationFactoryOptions Options => options.Value;

	private static ClaimsPrincipal CreateHostPrincipal(string appName, IReadOnlyList<string> roles) {
		var identity = new ClaimsIdentity(HostInvocationDefaults.Scheme);
		identity.AddClaim(new Claim("sub", appName));
		identity.AddClaim(new Claim("name", appName));
		AddRoles(identity, roles);
		return new ClaimsPrincipal(identity);
	}

	private static ClaimsPrincipal CreateContinuationPrincipal(ClaimsPrincipal user, IReadOnlyList<string> roles) {

		if (user.Identity?.IsAuthenticated != true) {
			throw new InvalidOperationException(UnauthenticatedUserMessage);
		}

		// The caller's principal is never modified: downstream enrichment adds claims to the
		// identity it is handed, so the invocation gets its own copy of every identity.
		var principal = new ClaimsPrincipal(user.Identities.Select(identity => identity.Clone()));

		if (roles.Count > 0) {
			var hostIdentity = new ClaimsIdentity(HostInvocationDefaults.Scheme);
			AddRoles(hostIdentity, roles);
			principal.AddIdentity(hostIdentity);
		}

		return principal;

	}

	private static void AddRoles(ClaimsIdentity identity, IReadOnlyList<string> roles) {
		foreach (var role in roles) {
			if (!string.IsNullOrWhiteSpace(role)) {
				identity.AddClaim(new Claim(identity.RoleClaimType, role));
			}
		}
	}

}
