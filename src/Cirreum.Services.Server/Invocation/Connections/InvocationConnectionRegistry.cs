namespace Cirreum.Invocation.Connections;

using Cirreum.Security;
using System.Collections.Concurrent;

/// <summary>
/// In-memory <see cref="IInvocationConnectionRegistry"/> — the per-server index of active
/// long-lived connections, fed by <see cref="ConnectionRegistryLifecycle"/> through the
/// already-firing <see cref="IConnectionLifecycle"/> hooks. Consumed by
/// <see cref="ConnectionTerminationHandler"/> to act on auth events.
/// </summary>
/// <remarks>
/// <para>
/// The registry stores no subject snapshot. <see cref="FindBySubject"/> resolves each
/// connection's subject at <em>query</em> time — <c>ClaimsHelper.ResolveId</c> (the same
/// resolution behind <c>IUserState.Id</c>, so publisher-side <c>Subject</c> values and
/// registry lookups agree by construction) over the connection's <c>EffectiveUser</c>
/// (see <see cref="InvocationConnectionExtensions"/>). A connection promoted mid-flight
/// via Two-Phase Auth is therefore attributed to the promoted identity from the moment
/// of promotion, with no re-registration call and no staleness window; before promotion
/// (no resolvable id) it matches no subject.
/// </para>
/// <para>
/// Termination is rare and diagnostics are off the hot path, so subject lookup is a
/// snapshot scan over active connections rather than a maintained secondary index —
/// simpler, and immune to index-vs-promotion races by construction.
/// </para>
/// </remarks>
internal sealed class InvocationConnectionRegistry : IInvocationConnectionRegistry {

	private readonly ConcurrentDictionary<string, IInvocationConnection> _connections = new(StringComparer.Ordinal);

	/// <inheritdoc />
	public void Register(IInvocationConnection connection) {
		ArgumentNullException.ThrowIfNull(connection);
		// Idempotent by overwrite on ConnectionId, per the contract.
		this._connections[connection.ConnectionId] = connection;
	}

	/// <inheritdoc />
	public void Unregister(string connectionId) {
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
		this._connections.TryRemove(connectionId, out _);
	}

	/// <inheritdoc />
	public IEnumerable<IInvocationConnection> FindBySubject(string subject) {
		ArgumentException.ThrowIfNullOrWhiteSpace(subject);
		return [.. this._connections.Values.Where(connection =>
			string.Equals(
				ClaimsHelper.ResolveId(connection.EffectiveUser),
				subject,
				StringComparison.Ordinal))];
	}

	/// <inheritdoc />
	public IEnumerable<IInvocationConnection> All() => [.. this._connections.Values];

	/// <inheritdoc />
	public IInvocationConnection? Find(string connectionId) {
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
		return this._connections.TryGetValue(connectionId, out var connection) ? connection : null;
	}

}
