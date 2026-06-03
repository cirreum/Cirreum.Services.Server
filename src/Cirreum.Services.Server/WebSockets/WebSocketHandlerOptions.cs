namespace Cirreum.Invocation.WebSockets;

/// <summary>
/// Per-handler tuning for a code-first WebSocket endpoint, supplied at the
/// <c>MapWebSocketHandler&lt;THandler&gt;(path, o =&gt; ...)</c> call site.
/// </summary>
/// <remarks>
/// <para>
/// WebSocket is composed code-first: the handler type is a code-supplied
/// dependency (it cannot come from appsettings), so its tuning lives in code too. This
/// type replaces the former appsettings-bound <c>WebSocketInvocationInstanceSettings</c>.
/// Apps that want configuration-driven limits bind their own <c>IConfiguration</c> inside
/// the <c>configure</c> callback (for example,
/// <c>o.MaxMessageSizeBytes = cfg.GetValue("...")</c>).
/// </para>
/// <para>
/// The framework fields the old settings carried — <c>Path</c>, <c>Scheme</c>,
/// <c>Enabled</c>, <c>RequestPath</c> — are gone: the path is the
/// <c>MapWebSocketHandler</c> argument, auth is applied via
/// <c>.RequireAuthorization(...)</c> on the returned endpoint, enablement is
/// "did you map it," and any pre-connection negotiate endpoint is a normal endpoint the
/// app maps itself (exposing the ASP.NET-native surface).
/// </para>
/// </remarks>
public sealed class WebSocketHandlerOptions {

	/// <summary>Default disconnect cleanup budget — 30 seconds.</summary>
	public const int DefaultDisconnectTimeoutSeconds = 30;

	/// <summary>Hard cap on disconnect cleanup budget — 5 minutes.</summary>
	public const int MaxDisconnectTimeoutSeconds = 300;

	/// <summary>Default max message size — 64 KB. Conservative default covering Twilio
	/// media streams, chat, telemetry, and most IoT protocols.</summary>
	public const int DefaultMaxMessageSizeBytes = 64 * 1024;

	/// <summary>Hard cap on max message size — 8 MB. Caps memory pressure under load.</summary>
	public const int MaxMaxMessageSizeBytes = 8 * 1024 * 1024;

	/// <summary>Default initial receive buffer size — 4 KB.</summary>
	public const int DefaultReceiveBufferSizeBytes = 4096;

	/// <summary>Hard cap on initial receive buffer size — 64 KB.</summary>
	public const int MaxReceiveBufferSizeBytes = 64 * 1024;

	/// <summary>
	/// Disconnect cleanup budget in seconds. Bounds how long
	/// <see cref="WebSocketHandler.OnDisconnectedAsync"/> and
	/// <c>IConnectionLifecycle.OnDisconnectedAsync</c> hooks have to complete before the
	/// framework cancels their tokens. Default 30s; hard cap 5 min.
	/// </summary>
	public int DisconnectTimeoutSeconds { get; set; } = DefaultDisconnectTimeoutSeconds;

	/// <summary>
	/// Maximum size in bytes of a single complete (possibly multi-frame) WebSocket message.
	/// Messages exceeding this are rejected with a <c>MessageTooBig</c> close frame.
	/// Default 64 KB; hard cap 8 MB.
	/// </summary>
	public int MaxMessageSizeBytes { get; set; } = DefaultMaxMessageSizeBytes;

	/// <summary>
	/// Initial size in bytes of the per-connection pooled receive buffer (rented from
	/// <c>ArrayPool&lt;byte&gt;.Shared</c>). Default 4 KB; hard cap 64 KB.
	/// </summary>
	public int ReceiveBufferSizeBytes { get; set; } = DefaultReceiveBufferSizeBytes;

	/// <summary>
	/// Keep-alive ping interval. When set, overrides the global
	/// <c>WebSocketOptions.KeepAliveInterval</c>. <see langword="null"/> inherits the global value.
	/// </summary>
	public TimeSpan? KeepAliveInterval { get; set; }

	/// <summary>
	/// Keep-alive timeout — how long to wait for a ping response before the connection is
	/// considered dead. When set, overrides the global <c>WebSocketOptions.KeepAliveTimeout</c>.
	/// <see langword="null"/> inherits the global value.
	/// </summary>
	public TimeSpan? KeepAliveTimeout { get; set; }

	/// <summary>
	/// Validates the configured limits against their hard caps. Called by
	/// <c>MapWebSocketHandler</c> at map time so misconfiguration fails fast at startup.
	/// </summary>
	internal void Validate() {

		if (this.DisconnectTimeoutSeconds <= 0
			|| this.DisconnectTimeoutSeconds > MaxDisconnectTimeoutSeconds) {
			throw new InvalidOperationException(
				$"{nameof(this.DisconnectTimeoutSeconds)} must be in (0, {MaxDisconnectTimeoutSeconds}]; got {this.DisconnectTimeoutSeconds}.");
		}

		if (this.MaxMessageSizeBytes <= 0
			|| this.MaxMessageSizeBytes > MaxMaxMessageSizeBytes) {
			throw new InvalidOperationException(
				$"{nameof(this.MaxMessageSizeBytes)} must be in (0, {MaxMaxMessageSizeBytes}]; got {this.MaxMessageSizeBytes}.");
		}

		if (this.ReceiveBufferSizeBytes <= 0
			|| this.ReceiveBufferSizeBytes > MaxReceiveBufferSizeBytes) {
			throw new InvalidOperationException(
				$"{nameof(this.ReceiveBufferSizeBytes)} must be in (0, {MaxReceiveBufferSizeBytes}]; got {this.ReceiveBufferSizeBytes}.");
		}
	}

}
