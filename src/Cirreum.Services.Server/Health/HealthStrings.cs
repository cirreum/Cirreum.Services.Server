namespace Cirreum.Health;

/// <summary>
/// Various string related to health check configuration and setup.
/// </summary>
public static class HealthStrings {

	/// <summary>
	/// Cirreum:HealthChecks:Enabled
	/// </summary>
	public static readonly string HealthChecksEnabledKey = "Cirreum:HealthChecks:Enabled";

	/// <summary>
	/// Cirreum:HealthChecks:BaseUri
	/// </summary>
	public static readonly string HealthCheckBaseUriKey = "Cirreum:HealthChecks:BaseUri";

	/// <summary>
	/// /health
	/// </summary>
	public static readonly string HealthDefaultBaseUriPath = "/health";

	/// <summary>
	/// /startup
	/// </summary>
	public static readonly string HealthStartupUriPath = "/startup";

	/// <summary>
	/// /liveness
	/// </summary>
	public static readonly string HealthLivenessUriPath = "/liveness";

	/// <summary>
	/// /readiness
	/// </summary>
	public static readonly string HealthReadinessUriPath = "/readiness";

	/// <summary>
	/// /internal
	/// </summary>
	public static readonly string HealthInternalUriPath = "/internal";

	/// <summary>
	/// startup tag
	/// </summary>
	public static readonly string HealthStartupTag = "startup";

	/// <summary>
	/// alive tag
	/// </summary>
	public static readonly string HealthLivenessTag = "alive";

	/// <summary>
	/// ready tag
	/// </summary>
	public static readonly string HealthReadinessTag = "ready";

}