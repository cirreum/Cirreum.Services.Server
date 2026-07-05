namespace Cirreum.Invocation.Connections;

using Cirreum.Authentication.Events;

/// <summary>
/// The framework-shipped connection terminator (ADR-0027 Phase B). Reacts to
/// <see cref="CredentialRevoked"/>, <see cref="UserAccountDisabled"/>, and
/// <see cref="SessionTerminationRequested"/> by looking up the subject's live long-lived
/// connections in <see cref="IInvocationConnectionRegistry"/> and calling the idempotent
/// <see cref="IInvocationConnection.Abort"/> on each match — completing the revocation
/// story so a revoked or force-signed-out subject loses their live WebSocket / SignalR
/// connections, not just future requests.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CredentialRevoked"/> and <see cref="UserAccountDisabled"/> abort <em>all</em>
/// of the subject's connections — the conservative posture: any of them may have been
/// established by the revoked credential, and a disabled account's sessions must not
/// outlive the disablement.
/// </para>
/// <para>
/// <see cref="SessionTerminationRequested"/> honors <c>SessionId</c> when present: only
/// subject-matching connections whose <see cref="IInvocationConnection.ConnectionId"/>
/// equals it (the "sign out this device" flow — the app reads the target's connection id
/// off <c>IInvocationContext.Connection</c>) or whose effective principal carries an
/// OIDC <c>sid</c> claim equal to it (browser-session scope) are aborted. The scope never
/// widens beyond the subject, and an unmatched scoped request terminates nothing — the
/// all-sessions switch is <c>SessionId = null</c>.
/// </para>
/// <para>
/// Subject and <c>sid</c> matching read the connection's <c>EffectiveUser</c>
/// (see <see cref="InvocationConnectionExtensions"/>), so connections promoted mid-flight
/// via Two-Phase Auth are terminable under their promoted identity.
/// </para>
/// <para>
/// Events fan out to every replica via the ADR-0025 delivery leg; each replica's handler
/// consults its local registry, so the subject's connections terminate fleet-wide without
/// any replica knowing about the others.
/// </para>
/// </remarks>
internal sealed partial class ConnectionTerminationHandler(
	IInvocationConnectionRegistry registry,
	ILogger<ConnectionTerminationHandler> logger
) : IAuthenticationEventHandler<CredentialRevoked>,
	IAuthenticationEventHandler<UserAccountDisabled>,
	IAuthenticationEventHandler<SessionTerminationRequested> {

	private const string SessionIdClaim = "sid";

	/// <inheritdoc />
	public ValueTask HandleAsync(CredentialRevoked evt, CancellationToken cancellationToken = default) {
		ArgumentNullException.ThrowIfNull(evt);
		this.TerminateAll(evt.Subject, nameof(CredentialRevoked));
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask HandleAsync(UserAccountDisabled evt, CancellationToken cancellationToken = default) {
		ArgumentNullException.ThrowIfNull(evt);
		this.TerminateAll(evt.Subject, nameof(UserAccountDisabled));
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask HandleAsync(SessionTerminationRequested evt, CancellationToken cancellationToken = default) {
		ArgumentNullException.ThrowIfNull(evt);

		if (evt.SessionId is null) {
			this.TerminateAll(evt.Subject, nameof(SessionTerminationRequested));
			return ValueTask.CompletedTask;
		}

		var matched = 0;
		foreach (var connection in registry.FindBySubject(evt.Subject)) {
			if (!IsSessionMatch(connection, evt.SessionId)) {
				continue;
			}
			matched++;
			LogTerminating(logger, connection.ConnectionId, connection.InvocationSource, evt.Subject, nameof(SessionTerminationRequested));
			connection.Abort();
		}

		if (matched == 0) {
			// A scoped request stays scoped: no fallback to all-sessions termination.
			LogNoScopedMatch(logger, evt.Subject, evt.SessionId);
		}

		return ValueTask.CompletedTask;
	}

	private void TerminateAll(string subject, string eventName) {
		foreach (var connection in registry.FindBySubject(subject)) {
			LogTerminating(logger, connection.ConnectionId, connection.InvocationSource, subject, eventName);
			connection.Abort();
		}
	}

	private static bool IsSessionMatch(IInvocationConnection connection, string sessionId) =>
		string.Equals(connection.ConnectionId, sessionId, StringComparison.Ordinal)
		|| string.Equals(
			connection.EffectiveUser.FindFirst(SessionIdClaim)?.Value,
			sessionId,
			StringComparison.Ordinal);

	[LoggerMessage(
		EventId = 1,
		Level = LogLevel.Information,
		Message = "Terminating connection {ConnectionId} ({InvocationSource}) for subject {Subject} in response to {AuthEvent}.")]
	private static partial void LogTerminating(
		ILogger logger,
		string connectionId,
		string invocationSource,
		string subject,
		string authEvent);

	[LoggerMessage(
		EventId = 2,
		Level = LogLevel.Debug,
		Message = "SessionTerminationRequested for subject {Subject} scoped to session {SessionId} matched no local connections; nothing terminated on this replica.")]
	private static partial void LogNoScopedMatch(
		ILogger logger,
		string subject,
		string sessionId);

}
