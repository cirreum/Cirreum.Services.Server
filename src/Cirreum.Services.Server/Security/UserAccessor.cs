namespace Cirreum.Security;

using Cirreum.RemoteServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;

/// <summary>
/// Default implementation of <see cref="IUserStateAccessor"/>
/// </summary>
sealed class UserAccessor(
	IHttpContextAccessor httpContextAccessor,
	IWebHostEnvironment webHostEnvironment,
	IServiceProvider serviceProvider
) : IUserStateAccessor {

	private const string UserContextKey = "__Cirreum_Context_UserState";
	private static readonly IUserState AnonymousUserInstance = new ServerUser();
	private static readonly ValueTask<IUserState> AnonymousUserValueTaskInstance =
		new ValueTask<IUserState>(AnonymousUserInstance);

	private readonly IHttpContextAccessor _httpContextAccessor =
		httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

	public ValueTask<IUserState> GetUser() {

		var context = this._httpContextAccessor.HttpContext;
		if (context == null) {
			return AnonymousUserValueTaskInstance;
		}

		// Check if we already have a UserState for this request
		if (context.Items.TryGetValue(UserContextKey, out var existingUser)
			&& existingUser is ServerUser user) {
			return new ValueTask<IUserState>(user);
		}

		var principal = context.User;
		if (principal?.Identity == null || !principal.Identity.IsAuthenticated) {
			context.Items[UserContextKey] = AnonymousUserInstance;
			return AnonymousUserValueTaskInstance;
		}

		// Create and enrich a new ServerUser
		// ----------------------------------

		return this.CreateUserAsync(context, principal);

	}

	private async ValueTask<IUserState> CreateUserAsync(HttpContext context, ClaimsPrincipal principal) {

		// Enrichment order matters — each step may depend on the previous:
		//   1. Claims enrichment  — adds app-name claims to the principal
		//   2. SetAuthenticatedPrincipal — builds the UserProfile from enriched claims
		//   3. ApplicationUser — resolves the domain user (may use Id from step 2)
		//   4. AuthenticationBoundary — resolves Global/Tenant (may inspect ApplicationUser from step 3)

		// 1. Pre-enrich the ClaimsPrincipal with app name from header if present
		string? appName = context.Request.Headers[RemoteIdentityConstants.AppNameHeader];
		if (!string.IsNullOrWhiteSpace(appName) &&
			principal.Identity is ClaimsIdentity identity) {
			var idName = ClaimsHelper.ResolveName(identity);
			if (string.IsNullOrWhiteSpace(idName)) {
				// This is for M2M scenarios where the identity may not have a
				// meaningful Name claim, so we use the app name as the Name
				// claim for easier identification in logs and diagnostics
				AddAppNameAsNameClaim(identity, appName);
			}
			AddAppNameToClaim(identity, appName);
		}

		// 2. Create a new ServerUser and set the authenticated principal
		var user = new ServerUser();
		user.SetAuthenticatedPrincipal(principal, appName ?? "", webHostEnvironment.IsDevelopment());

		// 3. Application user — cache hit (from claims transformer) or live resolve
		await this.ResolveApplicationUserAsync(user, context);

		// 4. Authentication boundary — Global (operator IdP) vs Tenant (customer IdP)
		ResolveAuthenticationBoundary(user, context);

		// Cache the fully-built user for the remainder of this request
		context.Items[UserContextKey] = user;

		return user;

	}

	private static void AddAppNameAsNameClaim(ClaimsIdentity identity, string appName) {

		// Remove the URI format name claim if it exists
		var uriNameClaim = identity.FindFirst(identity.NameClaimType);
		if (uriNameClaim != null) {
			identity.RemoveClaim(uriNameClaim);
		}

		// If the identity.NameClaimType isn't ClaimTypes.Name, check for that too
		if (identity.NameClaimType != ClaimTypes.Name) {
			var stdNameClaim = identity.FindFirst(ClaimTypes.Name);
			if (stdNameClaim != null) {
				identity.RemoveClaim(stdNameClaim);
			}
		}

		// Remove the simple string format name claim if it exists
		var simpleNameClaim = identity.FindFirst("name");
		if (simpleNameClaim != null) {
			identity.RemoveClaim(simpleNameClaim);
		}

		// Add the app name from header as the Name claim
		identity.AddClaim(new Claim(ClaimTypes.Name, appName));

	}
	private static void AddAppNameToClaim(ClaimsIdentity identity, string appName) {

		// Remove existing app name claim if present
		var existingAppNameClaim = identity.FindFirst(RemoteIdentityConstants.AppNameClaimType);
		if (existingAppNameClaim != null) {
			identity.RemoveClaim(existingAppNameClaim);
		}

		// Add the new app name claim
		identity.AddClaim(new Claim(RemoteIdentityConstants.AppNameClaimType, appName));

	}
	private async ValueTask ResolveApplicationUserAsync(ServerUser user, HttpContext context) {

		// Cache hit: ApplicationUserRoleResolverAdapter (run during claims transformation,
		// inside AuthenticateAsync) already resolved the application user and stashed it
		// here. Steady-state path for tenant-track requests.
		if (context.Items.TryGetValue(AuthenticationContextKeys.ApplicationUserCache, out var cached)
			&& cached is IApplicationUser cachedAppUser) {
			user.SetResolvedApplicationUser(cachedAppUser);
			return;
		}

		// Cache-miss fallback. Legitimately fires when:
		//   - Operator/machine-track requests — the transformer short-circuited via
		//     RolesAlreadyPresent (token already had roles), so the resolver was never
		//     invoked. No matching resolver should be registered for these schemes.
		//   - Tenant-track edge cases — transformer skipped via NoClaimsIdentity or
		//     NoUserIdentifier (no resolvable user-id claim).
		//   - Non-HTTP code paths that synthesize HttpContext without running claims
		//     transformation (test harnesses, internal dispatch).
		//
		// Dispatch to the resolver matching the request's authenticated scheme; falls
		// back to the null-scheme default. No matching resolver = correct null outcome.
		var resolvers = serviceProvider.GetServices<IApplicationUserResolver>();
		if (!resolvers.Any()) {
			return;
		}

		var scheme = context.Items[AuthenticationContextKeys.AuthenticatedScheme] as string
				  ?? user.Identity?.AuthenticationType;

		var resolver = resolvers.FirstOrDefault(r => r.Scheme == scheme)
					?? resolvers.FirstOrDefault(r => r.Scheme is null);

		if (resolver is not null) {
			var appUser = await resolver.ResolveAsync(user.Id);
			if (appUser is not null) {
				user.SetResolvedApplicationUser(appUser);
				context.Items[AuthenticationContextKeys.ApplicationUserCache] = appUser;
			}
		}

	}
	private static void ResolveAuthenticationBoundary(ServerUser user, HttpContext context) {
		var resolver = context.RequestServices.GetService<IAuthenticationBoundaryResolver>();
		if (resolver is null) {
			user.SetResolvedAuthenticationBoundary(AuthenticationBoundary.None);
			return;
		}

		var scheme = context.Items[AuthenticationContextKeys.AuthenticatedScheme] as string
					  ?? user.Identity?.AuthenticationType;

		var boundary = resolver.Resolve(user, scheme);
		user.SetResolvedAuthenticationBoundary(boundary);
	}

}