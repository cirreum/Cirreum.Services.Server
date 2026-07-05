namespace Cirreum.Security;

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
