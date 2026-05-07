namespace Cirreum.Http.Invocation;

using Cirreum.Invocation;

/// <summary>
/// Per-request middleware that materializes an <see cref="IInvocationContext"/> for the
/// active <see cref="HttpContext"/> and publishes it through
/// <see cref="IInvocationContextAccessor"/>. Pairs <c>Set</c> / <c>Clear</c> in a
/// try/finally for symmetric AsyncLocal lifetime.
/// </summary>
/// <remarks>
/// Register late — after <c>UseAuthentication</c> and <c>UseAuthorization</c>, before
/// endpoint execution — so the snapshotted <see cref="IInvocationContext.User"/>
/// reflects the fully-resolved authenticated principal.
/// </remarks>
internal sealed class InvocationContextHttpMiddleware(
	RequestDelegate next,
	IInvocationContextAccessor accessor
) {

	public async Task InvokeAsync(HttpContext context) {

		var invocation = new HttpInvocationContext(context);
		accessor.Set(invocation);
		try {
			await next(context);
		} finally {
			accessor.Clear();
		}

	}

}
