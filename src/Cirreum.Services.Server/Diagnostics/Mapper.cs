namespace Cirreum.Diagnostics;

using Cirreum.Exceptions;
using Cirreum.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security;
using System.Security.Authentication;

/// <summary>
/// Maps an Exception to an <see cref="ExceptionModel"/>.
/// </summary>
internal static class Mapper {

	public static readonly Dictionary<int, (string Type, string Title)> Defaults = new() {

		[400] = (
			"https://tools.ietf.org/html/rfc9110#section-15.5.1",
			"Bad Request"
		),

		[401] = (
			"https://tools.ietf.org/html/rfc9110#section-15.5.2",
			"Unauthorized"
		),

		[403] = (
			"https://tools.ietf.org/html/rfc9110#section-15.5.4",
			"Forbidden"
		),

		[404] = (
			"https://tools.ietf.org/html/rfc9110#section-15.5.5",
			"Not Found"
		),

		[405] = (
			"https://tools.ietf.org/html/rfc9110#section-15.5.6",
			"Method Not Allowed"
		),

		[406] = (
			"https://tools.ietf.org/html/rfc9110#section-15.5.7",
			"Not Acceptable"
		),

		[408] = (
			"https://tools.ietf.org/html/rfc9110#section-15.5.9",
			"Request Timeout"
		),

		[409] = (
			"https://tools.ietf.org/html/rfc9110#section-15.5.10",
			"Conflict"
		),

		[412] = (
			"https://tools.ietf.org/html/rfc9110#section-15.5.13",
			"Precondition Failed"
		),

		[415] = (
			"https://tools.ietf.org/html/rfc9110#section-15.5.16",
			"Unsupported Media Type"
		),

		[422] = (
			"https://tools.ietf.org/html/rfc9110#section-15.5.21",
			"Unprocessable Entity"
		),

		[426] = (
			"https://tools.ietf.org/html/rfc9110#section-15.5.22",
			"Upgrade Required"
		),

		[500] = (
			"https://tools.ietf.org/html/rfc9110#section-15.6.1",
			"An error occurred while processing your request."
		),

		[502] = (
			"https://tools.ietf.org/html/rfc9110#section-15.6.3",
			"Bad Gateway"
		),

		[503] = (
			"https://tools.ietf.org/html/rfc9110#section-15.6.4",
			"Service Unavailable"
		),

		[504] = (
			"https://tools.ietf.org/html/rfc9110#section-15.6.5",
			"Gateway Timeout"
		),
	};

	/// <summary>
	/// Maps an exception to an <see cref="ExceptionModel"/>
	/// </summary>
	/// <typeparam name="TException"><typeparamref name="TException"/>.</typeparam>
	/// <param name="exception">The source exception.</param>
	/// <param name="isDev">Is this the development environment.</param>
	/// <returns>The mapped <see cref="ExceptionModel"/></returns>
	public static ExceptionModel ToExceptionModel<TException>(
		this TException exception,
		bool isDev) where TException : Exception
		=> exception.ToExceptionModel(StatusCodes.Status500InternalServerError, isDev);
	/// <summary>
	/// Maps an exception to an <see cref="ExceptionModel"/>
	/// </summary>
	/// <typeparam name="TException"><typeparamref name="TException"/>.</typeparam>
	/// <param name="exception">The source exception.</param>
	/// <param name="statusCode">The <c>HttpContext.Response.StatusCode</c> for the current request.</param>
	/// <param name="isDev">Is this the development environment.</param>
	/// <returns>The mapped <see cref="ExceptionModel"/></returns>
	public static ExceptionModel ToExceptionModel<TException>(
		this TException exception,
		int statusCode,
		bool isDev) where TException : Exception {
		return exception switch {
			AuthenticationException authenticationException => FromAuthenticationException(authenticationException, isDev),
			UnauthenticatedAccessException unauthenticatedAccessException => FromUnauthenticatedAccessException(unauthenticatedAccessException, isDev),
			UnauthorizedAccessException unauthorizedAccessException => FromUnauthorizedAccessException(unauthorizedAccessException, isDev),
			ForbiddenAccessException forbiddenAccessException => FromForbiddenAccessException(forbiddenAccessException, isDev),
			SecurityException securityException => FromSecurityException(securityException, isDev),
			ConflictException conflictException => FromConflictException(conflictException),
			AlreadyExistsException alreadyExistsException => FromAlreadyExistException(alreadyExistsException),
			BadRequestException badRequestException => FromBadRequestException(badRequestException),
			BadHttpRequestException validationException => FromBadHttpRequestException(validationException, isDev),
			BatchOperationException batchOperationException => FromBatchOperationException(batchOperationException),
			ConcurrencyException concurrencyException => FromConcurrencyException(concurrencyException),
			NotFoundException notFoundException => FromNotFoundException(notFoundException),
			KeyNotFoundException keyNotFoundException => FromKeyNotFoundException(keyNotFoundException),
			ValidationException validationException => FromValidationException(validationException),
			_ => FromUnknownException(exception, statusCode, isDev)
		};
	}

	internal static void ApplyDefaults(this ExceptionModel model, HttpContext context) {

		if (Defaults.TryGetValue(model.Status, out var defaults)) {
			model.Title ??= defaults.Title;
			model.Type ??= defaults.Type;
		} else {
			var reasonPhrase = ReasonPhrases.GetReasonPhrase(model.Status);
			if (string.IsNullOrEmpty(reasonPhrase) is false) {
				model.Title = reasonPhrase;
			}
		}

		model.Instance ??= context.Request.Path;

	}

	private static ExceptionModel FromUnknownException<TException>(TException e, int status, bool isDev) where TException : Exception {
		// Don't trust 2XX or unset status codes for exceptions
		var effectiveStatus = status >= 400 ? status : 500;
		return new ExceptionModel {
			Status = effectiveStatus,
			Detail = isDev ? e.Message : ""
		};
	}

	private static ExceptionModel FromAuthenticationException(AuthenticationException exception, bool isDev) {
		return new ExceptionModel {
			StatusCode = HttpStatusCode.Unauthorized,
			Detail = isDev ? exception.Message : "Authentication required"
		};
	}

	private static ExceptionModel FromUnauthenticatedAccessException(UnauthenticatedAccessException exception, bool isDev) {
		return new ExceptionModel {
			StatusCode = HttpStatusCode.Unauthorized,
			Detail = isDev ? exception.Message : "Authentication required"
		};
	}

	private static ExceptionModel FromUnauthorizedAccessException(UnauthorizedAccessException exception, bool isDev) {
		return new ExceptionModel {
			StatusCode = HttpStatusCode.Forbidden,
			Detail = isDev ? exception.Message : "Access denied"
		};
	}

	private static ExceptionModel FromForbiddenAccessException(ForbiddenAccessException exception, bool isDev) {
		return new ExceptionModel {
			StatusCode = HttpStatusCode.Forbidden,
			Detail = isDev ? exception.Message : "Access denied"
		};
	}

	private static ExceptionModel FromSecurityException(SecurityException exception, bool isDev) {
		return new ExceptionModel {
			StatusCode = HttpStatusCode.Forbidden,
			Detail = isDev ? exception.Message : "Access denied"
		};
	}

	private static ExceptionModel FromAlreadyExistException(AlreadyExistsException exception) {

		var errors = exception.InnerException?.Message.Split(';') ?? [];
		return new ExceptionModel {
			StatusCode = HttpStatusCode.Conflict,
			Detail = exception.Message,
			Failures = [.. errors.Select(x => new FailureModel {
				ErrorMessage = x,
				ErrorCode = $"{HttpStatusCode.Conflict}",
				Severity = FailureSeverity.Error
			})]
		};

	}

	private static ExceptionModel FromConflictException(ConflictException exception) {
		return new ExceptionModel {
			StatusCode = HttpStatusCode.Conflict,
			Detail = exception.Message
		};
	}

	private static ExceptionModel FromBadHttpRequestException(BadHttpRequestException exception, bool isDev) {

		return new ExceptionModel {
			StatusCode = HttpStatusCode.BadRequest,
			Detail = isDev ? exception.Message : "Unable to process the request"
		};

	}

	private static ExceptionModel FromBadRequestException(BadRequestException exception) {

		return new ExceptionModel {
			StatusCode = HttpStatusCode.BadRequest,
			Detail = exception.Message
		};

	}

	private static ExceptionModel FromBatchOperationException(BatchOperationException exception) {

		return new ExceptionModel {
			StatusCode = exception.StatusCode,
			Detail = exception.Message
		};

	}

	private static ExceptionModel FromConcurrencyException(ConcurrencyException exception) {

		return new ExceptionModel {
			StatusCode = HttpStatusCode.PreconditionFailed,
			Detail = exception.Message
		};

	}

	private static ExceptionModel FromNotFoundException(NotFoundException exception) {

		return new ExceptionModel {
			StatusCode = HttpStatusCode.NotFound,
			Detail = exception.Message
		};

	}

	private static ExceptionModel FromKeyNotFoundException(KeyNotFoundException exception) {

		return new ExceptionModel {
			StatusCode = HttpStatusCode.BadRequest,
			Detail = exception.Message
		};

	}

	private static ExceptionModel FromValidationException(ValidationException exception) {

		return new ExceptionModel {
			StatusCode = HttpStatusCode.UnprocessableEntity,
			Detail = exception.Message,
			Failures = [.. exception.Errors.Select(e => e.ToFailureModel())],
		};

	}

}