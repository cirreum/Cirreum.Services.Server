namespace Cirreum.Security;

using Cirreum.Authentication;
using Cirreum.Http.Invocation;
using Cirreum.Invocation;
using Cirreum.RemoteServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;

/// <summary>
/// Default implementation of <see cref="IUserStateAccessor"/>
/// </summary>
sealed class UserStateAccessor(
	IInvocationContextAccessor invocationAccessor,
	IWebHostEnvironment webHostEnvironment
) : IUserStateAccessor {

	private const string UserContextKey = "__Cirreum_Context_UserState";
	private static readonly IUserState AnonymousUserInstance = new ServerUserState();
	private static readonly ValueTask<IUserState> AnonymousUserValueTaskInstance =
		new ValueTask<IUserState>(AnonymousUserInstance);

	public ValueTask<IUserState> GetUserState() {

		var invocation = invocationAccessor.Current;
		if (invocation == null) {
			return AnonymousUserValueTaskInstance;
		}

		// Check if we already have a UserState for this invocation
		if (invocation.Items.TryGetValue(UserContextKey, out var existingUser)
			&& existingUser is ServerUserState user) {
			return new ValueTask<IUserState>(user);
		}

		var principal = invocation.User;
		if (principal?.Identity == null || !principal.Identity.IsAuthenticated) {
			invocation.Items[UserContextKey] = AnonymousUserInstance;
			return AnonymousUserValueTaskInstance;
		}

		// Create and enrich a new ServerUserState
		// ----------------------------------

		return this.CreateUserAsync(invocation, principal);

	}

	private async ValueTask<IUserState> CreateUserAsync(IInvocationContext invocation, ClaimsPrincipal principal) {

		// Enrichment order matters — each step may depend on the previous:
		//   0. SubjectKind        — the effective scheme's declaration; gates the app-name fallback
		//   1. Claims enrichment  — fill-only app-name claims on the principal
		//   2. SetAuthenticatedPrincipal — builds the UserProfile from enriched claims
		//   3. ApplicationUser — resolves the domain user (may use Id from step 2)
		//   4. AuthenticationBoundary — resolves Global/Tenant (may inspect ApplicationUser from step 3)

		// 0. Subject kind — the effective scheme (origin ?? authenticated) is the scheme that
		// established the subject; its declaration answers person-or-machine. Resolved
		// optionally: with no registered map every scheme is Undeclared and the kind stays
		// Unknown.
		var map = invocation.Services.GetService<ISchemeClaimAuthorityMap>();
		var subjectKind = map?.Get(invocation.EffectiveScheme).SubjectKind ?? SubjectKind.Unknown;

		// 1. Pre-enrich the ClaimsPrincipal with the caller-supplied app name. Fill-only: the
		// header is unauthenticated, so it may add a Name claim where the credential minted
		// none, and never removes or overwrites credential-derived claims. The machine gate
		// applies once a declaration map is registered; without one the subject kind is
		// unresolvable and the legacy blank-name gate stands.
		var appName = (invocation as HttpInvocationContext)?.AppName;
		if (!string.IsNullOrWhiteSpace(appName) &&
			principal.Identity is ClaimsIdentity identity) {
			if ((map is null || subjectKind is SubjectKind.Machine)
				&& string.IsNullOrWhiteSpace(ClaimsHelper.ResolveName(identity))) {
				identity.AddClaim(new Claim(ClaimTypes.Name, appName));
			}
			AddAppNameToClaim(identity, appName);
		}

		// 2. Create a new ServerUserState and set the authenticated principal
		var user = new ServerUserState();
		user.SetAuthenticatedPrincipal(principal, appName ?? "", webHostEnvironment.IsDevelopment());
		user.SetResolvedSubjectKind(subjectKind);

		// 3. Application user — cache hit (from claims transformer) or live resolve
		await ResolveApplicationUserAsync(user, invocation);

		// 4. Authentication boundary — Global (operator IdP) vs Tenant (customer IdP)
		ResolveAuthenticationBoundary(user, invocation);

		// Cache the fully-built user for the remainder of this invocation
		invocation.Items[UserContextKey] = user;

		return user;

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
	private static async ValueTask ResolveApplicationUserAsync(ServerUserState user, IInvocationContext invocation) {

		// Cache hit: ApplicationUserRoleResolverAdapter (run during claims transformation,
		// inside AuthenticateAsync) already resolved the application user and stashed it
		// here. Steady-state path for tenant-track requests.
		if (invocation.Items.TryGetValue(AuthenticationContextKeys.ApplicationUserCache, out var cached)
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
		// Dispatch to the resolver matching the subject's effective scheme — the origin
		// when the subject was established by another scheme (a ticket continuation or a
		// promotion), else the authenticated scheme; falls back to the null-scheme default.
		// No matching resolver = correct null outcome.
		var resolvers = invocation.Services.GetServices<IApplicationUserResolver>();
		if (!resolvers.Any()) {
			user.SetResolvedApplicationUser(null);
			return;
		}

		var scheme = invocation.EffectiveScheme
				  ?? user.Principal.Identity?.AuthenticationType;

		var resolver = resolvers.FirstOrDefault(r => r.Scheme == scheme)
					?? resolvers.FirstOrDefault(r => r.Scheme is null);

		if (resolver is not null) {
			var appUser = await resolver.ResolveAsync(user.Id);
			if (appUser is not null) {
				user.SetResolvedApplicationUser(appUser);
				invocation.Items[AuthenticationContextKeys.ApplicationUserCache] = appUser;
				// Connection-lifetime write-back: THE re-hydration leg after Two-Phase Auth
				// promotion. connection.Promote(principal, originScheme) evicts
				// ApplicationUserCache from Connection.Items (the cached user belonged to the
				// pre-promotion identity), so the next invocation seeds nothing, misses above,
				// and lands here — the resolver dispatch keyed on the effective scheme (the
				// promoted subject's origin when stamped, else the connection's authenticated
				// scheme) resolves for the PROMOTED subject (user.Id comes from
				// EffectiveUser), and this write-back re-populates the connection bag so
				// subsequent invocations seed the promoted identity's user: one resolver
				// call per promotion, not per message. Also serves any future long-lived
				// source whose lazy resolve goes live (null-scheme resolver for header-auth
				// or M2M-on-behalf-of-human — the AI/LLM Piece 2 seam). For audience-auth
				// connections that never promote, the upgrade pre-populates the cache and
				// this branch never fires.
				if (invocation.Connection is { } connection) {
					connection.Items[AuthenticationContextKeys.ApplicationUserCache] = appUser;
				}
				return;
			}
		}

		user.SetResolvedApplicationUser(null);

	}
	private static void ResolveAuthenticationBoundary(ServerUserState user, IInvocationContext invocation) {
		var resolver = invocation.Services.GetService<IAuthenticationBoundaryResolver>();
		if (resolver is null) {
			user.SetResolvedAuthenticationBoundary(AuthenticationBoundary.None);
			return;
		}

		var scheme = invocation.EffectiveScheme
					  ?? user.Principal.Identity?.AuthenticationType;

		var boundary = resolver.Resolve(user, scheme);
		user.SetResolvedAuthenticationBoundary(boundary);
	}

}
