namespace Cirreum.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Interface used for our internal application-started indicator.
/// </summary>
internal interface IStartedAndAliveHealthCheck : IHealthCheck, IStartedStatus;