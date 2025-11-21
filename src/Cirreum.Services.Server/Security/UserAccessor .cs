namespace Cirreum.Security;

using Cirreum.RemoteServices;
using System.Security.Claims;

/// <summary>
/// Default implementation of <see cref="IUserStateAccessor"/>
/// </summary>
sealed class UserAccessor(
	IHttpContextAccessor httpContextAccessor
) : IUserStateAccessor {

	private const string UserContextKey = "__User_Context_Key";
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

		string? appName = context.Request.Headers[RemoteIdentityConstants.AppNameHeader];
		if (!string.IsNullOrWhiteSpace(appName) &&
			principal.Identity is ClaimsIdentity identity) {
			var idName = ClaimsHelper.ResolveName(identity);
			if (string.IsNullOrWhiteSpace(idName)) {
				AddAppNameAsNameClaim(identity, appName);
			}
			AddAppNameToClaim(identity, appName);
		}
		user = new ServerUser();
		user.SetAuthenticatedPrincipal(principal, appName ?? "");
		context.Items[UserContextKey] = user;
		return new ValueTask<IUserState>(user);

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

}