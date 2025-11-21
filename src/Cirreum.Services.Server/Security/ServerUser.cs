namespace Cirreum.Security;

using System.Security.Claims;

/// <summary>
/// Extends <see cref="UserStateBase"/> with an additional property
/// of <see cref="AppName"/>.
/// </summary>
public abstract class ServerUserBase : UserStateBase {

	/// <summary>
	/// Gets the name of the application that called the server; or any empty string.
	/// </summary>
	public string AppName { get; protected set; } = "";

}

internal sealed class ServerUser : ServerUserBase {

	internal void SetAuthenticatedPrincipal(ClaimsPrincipal principal, string appName) {
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
		if (!this.SessionStartTime.HasValue) {
			this.StartSession();
		}
	}

	internal void SetAnonymous() {
		this._isAuthenticated = false;
		this._principal = AnonymousUser.Shared;
		this._identity = AnonymousUser.Shared.Identity;
		this._profile = UserProfile.Anonymous;
		this.EndSession();
	}

}