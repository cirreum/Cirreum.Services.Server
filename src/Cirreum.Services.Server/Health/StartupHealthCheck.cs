namespace Cirreum.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// A custom health check, that returns healthy if the application
/// has started successfully (See: <see cref="IStartedStatus.StartupCompleted"/>).
/// </summary>
internal sealed class StartupHealthCheck : IStartedAndAliveHealthCheck {

	internal static string Name = nameof(StartupHealthCheck);
	/// <summary>
	/// startup
	/// </summary>
	internal static string Tag = "startup";

	private volatile bool _hasStarted;

	/// <inheritdoc/>
	public bool StartupCompleted {
		get => _hasStarted;
		set => _hasStarted = value;
	}

	/// <inheritdoc/>
	public Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default) {

		if (this._hasStarted) {
			return Task.FromResult(HealthCheckResult.Healthy("Application has started"));
		}

		return Task.FromResult(HealthCheckResult.Degraded("That startup task is still running"));

	}

}