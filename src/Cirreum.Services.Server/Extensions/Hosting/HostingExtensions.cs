namespace Microsoft.Extensions.DependencyInjection;

using Cirreum;
using Cirreum.Clock;
using Cirreum.Conductor.Caching;
using Cirreum.Diagnostics;
using Cirreum.Health;
using Cirreum.Messaging;
using Cirreum.Security;
using Humanizer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

public static class HostingExtensions {

	/// <summary>
	/// Add the single and global <see cref="GlobalUnhandledExceptionHandler"/>.
	/// </summary>
	/// <param name="services"></param>
	/// <returns>The source <see cref="IServiceCollection"/> instance.</returns>
	public static IServiceCollection AddGlobalExceptionHandling(this IServiceCollection services) {

		services
			.AddExceptionHandler(o => {
				o.ExceptionHandler = context => Task.CompletedTask;
			})
			.AddExceptionHandler<GlobalUnhandledExceptionHandler>();

		// Use IConfigureOptions (instead of post-configure) so the registration gets added/invoked
		// relative to when AddProblemDetails() is called.
		var svc = ServiceDescriptor.Singleton<IConfigureOptions<JsonOptions>, ExceptionModelJsonOptionsSetup>();
		services.TryAddEnumerable(svc);

		return services;

	}

	/// <summary>
	/// Registers and configures the core services.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/>.</param>
	/// <returns>The <see cref="IServiceCollection"/>.</returns>
	public static IServiceCollection AddCoreServices(this IServiceCollection services) {

		//
		// HttpContextAccessor & IUserContextAccessor
		//
		services
			.AddHttpContextAccessor()
			.AddScoped<IUserStateAccessor, UserAccessor>();

		//
		// Default IEnvironment implementation
		//
		services
			.TryAddSingleton<IEnvironment>(SystemEnvironment.Instance);

		//
		// DateTime/Clock
		//
		services
			.TryAddSingleton(TimeProvider.System);
		services
			.TryAddSingleton<IDateTimeClock, DateTimeService>();


		//
		// File System
		//
		services
			.AddTransient<ICsvFileBuilder, CsvFileBuilder>()
			.AddTransient<ICsvFileReader, CsvFileReader>()
			.AddTransient<IFileSystem, LocalFileSystem>();

		//
		// Distributed Messaging
		//
		services
			.TryAddSingleton<IDistributedTransportPublisher, EmptyTransportPublisher>();

		//
		// Conductor Cacheable Query Service (HybridCachce)
		//
		services
			.TryAddSingleton<ICacheableQueryService, HybridCacheableQueryService>();

		return services;

	}

	/// <summary>
	/// Adds Health Check services to the container.
	/// </summary>
	/// <param name="services"></param>
	/// <returns></returns>
	/// <remarks>
	/// <para>
	/// Also adds a default <see cref="IStartedStatus"/> health check, which is used for a
	/// "startup" probe. The health check status (<see cref="IStartedStatus.StartupCompleted"/>) is
	/// set to <see langword="true"/> once the application successfully starts via the built
	/// <c>WebApplication</c> with the extension method <c>InitializeAndRunAsync</c>.
	/// </para>
	/// <para>
	/// If the <c>Cirreum.Runtime</c> is not used or the extension method <c>InitializeAndRunAsync</c>
	/// is not used, you will need a service or some other way to set the status to true.
	/// <code>
	///	var startupStatus = webApplication.Services.GetRequiredService&lt;IStartedStatus&gt;();
	/// // do startup work...
	/// // ... starting
	/// // Ok, we've started!
	/// startupStatus.StartupCompleted = true;
	/// </code>
	/// </para>
	/// </remarks>
	public static IHealthChecksBuilder AddDefaultHealthChecks(this IServiceCollection services) {

		//
		// Default Startup HealthCheck
		//
		var startedHealthCheck = new StartupHealthCheck();
		return services
			.AddSingleton<IStartedStatus>(sp => startedHealthCheck)
			.AddSingleton<IStartedAndAliveHealthCheck>(sp => startedHealthCheck)
			.AddHealthChecks()
			.AddCheck<IStartedAndAliveHealthCheck>(
				StartupHealthCheck.Name.Kebaberize(),
				tags: [StartupHealthCheck.Tag]);

	}

}
