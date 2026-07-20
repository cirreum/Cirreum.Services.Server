namespace Cirreum.Security;

using System.Security.Claims;

/// <summary>
/// The server-side <see cref="IUserState"/> implementation, materialized per invocation by
/// <see cref="UserStateAccessor"/> from the invocation's snapshotted principal.
/// </summary>
internal sealed class ServerUserState : ServerUserBase {

	public override bool IsAuthenticationComplete { get; } = true;

	internal void SetAuthenticatedPrincipal(ClaimsPrincipal principal, string appName, bool isDevelopment) {

		this.AppName = appName;

		ArgumentNullException.ThrowIfNull(principal);
		this._principal = principal;

		if (this._principal.Identity is not ClaimsIdentity claimsIdentity) {
			throw new InvalidOperationException($"{nameof(principal)} Identity is null or not a ClaimsIdentity.");
		}
		this._identity = claimsIdentity;

		this._isAuthenticated = this._identity.IsAuthenticated;
		if (!this._isAuthenticated) {
			throw new InvalidOperationException("Cannot initialize from an unauthenticated user. Use SetAnonymous method.");
		}

		this._profile = new UserProfile(this._principal, TimeZoneInfo.Local.Id);

		ClaimsUserProfileEnricher.EnrichProfile(this._profile, claimsIdentity, captureUnknownClaims: isDevelopment);
		this.EnrichmentComplete();

		if (!this.SessionStartTime.HasValue) {
			this.StartSession();
		}

	}

	internal void SetResolvedApplicationUser(IApplicationUser? applicationUser) {
		this.SetApplicationUser(applicationUser);
	}

	internal void SetResolvedAuthenticationBoundary(AuthenticationBoundary boundary) {
		this.SetAuthenticationBoundary(boundary);
	}

	internal void SetAnonymous() {
		this._isAuthenticated = false;
		this._principal = AnonymousUser.Shared;
		this._identity = AnonymousUser.Shared.Identity;
		this._profile = UserProfile.Anonymous;
		this.EndSession();
	}

}
