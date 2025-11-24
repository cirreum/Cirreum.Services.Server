namespace Cirreum.Http.Filters;

using Cirreum.Diagnostics;     // ToExceptionModel / ApplyDefaults
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DomainResult = IResult;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Endpoint filter for Minimal APIs that converts Cirreum results and escaped exceptions
/// into HTTP responses.
/// </summary>
public sealed class ResultToHttpEndpointFilter(
	IHostEnvironment environment,
	ILogger<ResultToHttpEndpointFilter> logger
) : IEndpointFilter {

	public async ValueTask<object?> InvokeAsync(
		EndpointFilterInvocationContext context,
		EndpointFilterDelegate next) {
		try {
			var result = await next(context);
			// 1. Already an HTTP result? Leave it alone.
			if (result is HttpResult httpResult) {
				return httpResult;
			}
			// 2. Cirreum Result or Result<T>
			if (result is DomainResult cirreumResult) {
				return this.MapDomainResult(cirreumResult, context.HttpContext);
			}
			// 3. Any other type – let Minimal APIs serialize it normally.
			return result;
		} catch (Exception ex) {
			// Exceptions that escape the railway still go through your
			// exception → problem+json mapper.
			return this.MapFailure(ex, context.HttpContext);
		}
	}

	private HttpResult MapDomainResult(DomainResult result, HttpContext httpContext) {
		if (result.IsSuccess) {
			var value = result.GetValue();
			return value is null
				? TypedResults.NoContent()
				: TypedResults.Ok(value);
		}
		return this.MapFailure(result.Error!, httpContext);
	}

	private JsonHttpResult<ExceptionModel> MapFailure(Exception error, HttpContext httpContext) {
		var model = error.ToExceptionModel(
			httpContext.Response.StatusCode,
			environment.IsDevelopment());
		model.ApplyDefaults(httpContext);
		if (logger.IsEnabled(LogLevel.Debug)) {
			var errorType = error.GetType().Name;
			logger.LogDebug(
				"Result failure with {ExceptionType} (Status: {StatusCode}) for {Path}",
				errorType,
				model.Status,
				httpContext.Request.Path);
		}
		return TypedResults.Json(
			model,
			statusCode: model.Status,
			contentType: "application/problem+json");
	}

}