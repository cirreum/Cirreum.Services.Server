namespace Cirreum.Diagnostics;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A default exception handler that processes unhandled exceptions into standardized problem details responses.
/// Integrates with the Cirreum Exceptions Model and provides special handling for authentication and authorization failures.
/// </summary>
/// <remarks>
/// <para>
/// Authentication failures (status 401) are handled by invoking <see cref="AuthenticationHttpContextExtensions.ChallengeAsync(HttpContext)"/>.
/// If a policy with specific authentication schemes is found, each scheme is challenged individually using 
/// <see cref="AuthenticationHttpContextExtensions.ChallengeAsync(HttpContext, string?)"/>.
/// </para>
/// <para>
/// Authorization failures (status 403) are handled by invoking <see cref="AuthenticationHttpContextExtensions.ForbidAsync(HttpContext)"/>.
/// If a policy with specific authentication schemes is found, each scheme is forbidden individually using
/// <see cref="AuthenticationHttpContextExtensions.ForbidAsync(HttpContext, string?)"/>.
/// </para>
/// <para>
/// All other exceptions are converted to Problem Details format (RFC 7807) with appropriate status codes
/// and detailed error information that respects the current environment (development/production).
/// </para>
/// </remarks>
public sealed class GlobalUnhandledExceptionHandler(
	IServiceProvider serviceProvider
) : IExceptionHandler {

	private readonly IAuthorizationPolicyProvider? _policyProvider =
		serviceProvider.GetService<IAuthorizationPolicyProvider>();
	private static readonly MediaTypeHeaderValue _jsonMediaType = new("application/json");
	private static readonly MediaTypeHeaderValue _problemDetailsJsonMediaType = new("application/problem+json");

	public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) {

		// Let cancellation bubble to runtime - client is gone anyway
		if (exception is OperationCanceledException || httpContext.RequestAborted.IsCancellationRequested) {
			return false;
		}

		if (CanWrite(httpContext) is false) {
			return false;
		}

		var jsonOptions = httpContext.RequestServices.GetService<IOptions<JsonOptions>>();
		if (jsonOptions is null || jsonOptions.Value is null) {
			return false;
		}
		var serializerOptions = jsonOptions.Value.SerializerOptions;

		var env = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();
		var isDev = env.IsDevelopment();

		var model = exception.ToExceptionModel(httpContext.Response.StatusCode, isDev);
		var policy = this._policyProvider is null
			? null
			: await GetCurrentAuthorizationPolicy(this._policyProvider, httpContext);

		//
		// Handle Unauthenticated directly...
		//
		if (model.Status is 401) {
			httpContext.Response.Clear();
			httpContext.Response.StatusCode = model.Status;
			if (policy?.AuthenticationSchemes.Count > 0) {
				foreach (var scheme in policy.AuthenticationSchemes) {
					await httpContext.ChallengeAsync(scheme);
				}
			} else {
				await httpContext.ChallengeAsync();
			}
			return true;
		}

		//
		// Handle Unauthorized directly...
		//
		if (model.Status is 403) {
			httpContext.Response.Clear();
			httpContext.Response.StatusCode = model.Status;
			if (policy?.AuthenticationSchemes.Count > 0) {
				foreach (var scheme in policy.AuthenticationSchemes) {
					await httpContext.ForbidAsync(scheme);
				}
			} else {
				await httpContext.ForbidAsync();
			}
			return true;
		}

		//
		// Fallback to ProblemDetail
		//
		model.ApplyDefaults(httpContext);
		httpContext.Response.StatusCode = model.Status;
		await httpContext.Response.WriteAsJsonAsync(
			model,
			serializerOptions.GetTypeInfo(model.GetType()),
			contentType: "application/problem+json",
			cancellationToken: cancellationToken);

		return true;

	}

	public static bool CanWrite(HttpContext httpContext) {

		var headers = new RequestHeaders(httpContext.Request.Headers);

		var acceptHeader = headers.Accept;

		// Based on https://www.rfc-editor.org/rfc/rfc7231#section-5.3.2 a request
		// without the Accept header implies that the user agent
		// will accept any media type in response
		if (acceptHeader.Count == 0) {
			return true;
		}

		for (var i = 0; i < acceptHeader.Count; i++) {
			var acceptHeaderValue = acceptHeader[i];

			if (_jsonMediaType.IsSubsetOf(acceptHeaderValue) ||
				_problemDetailsJsonMediaType.IsSubsetOf(acceptHeaderValue)) {
				return true;
			}
		}

		return false;
	}

	private static async Task<AuthorizationPolicy?> GetCurrentAuthorizationPolicy(
		IAuthorizationPolicyProvider policyProvider,
		HttpContext context) {

		// Try to get endpoint first
		var endpoint = context.GetEndpoint() ??
					  context.Features.Get<IEndpointFeature>()?.Endpoint;

		if (endpoint != null) {
			var authorizeData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
			if (authorizeData.Any()) {
				return await AuthorizationPolicy.CombineAsync(policyProvider, authorizeData);
			}
		}

		// If no endpoint or no auth data, try default policy
		var defaultPolicy = await policyProvider.GetDefaultPolicyAsync();
		if (defaultPolicy != null) {
			return defaultPolicy;
		}

		// Finally, try fallback policy
		return await policyProvider.GetFallbackPolicyAsync();

	}
}