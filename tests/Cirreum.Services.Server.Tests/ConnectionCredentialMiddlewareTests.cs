namespace Cirreum.Services.Server.Tests;

using Cirreum.Http.Invocation;
using Cirreum.Invocation.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

/// <summary>
/// Proofs for the promotion of a query-carried bearer credential into the
/// <c>Authorization</c> header, and for the scoping that keeps it off ordinary endpoints.
/// </summary>
public sealed class ConnectionCredentialMiddlewareTests {

	private sealed class DummyHub : Hub {
	}

	private sealed class TestEndpointFeature(Endpoint endpoint) : IEndpointFeature {
		public Endpoint? Endpoint { get; set; } = endpoint;
	}

	private static HttpContext Request(
		string? authorization = null,
		string? accessTokenQuery = null,
		bool connectionEndpoint = false,
		bool hubEndpoint = false,
		bool anyEndpoint = true) {

		var context = new DefaultHttpContext();

		if (authorization is not null) {
			context.Request.Headers.Authorization = authorization;
		}

		if (accessTokenQuery is not null) {
			context.Request.QueryString = new QueryString($"?access_token={accessTokenQuery}");
		}

		if (anyEndpoint) {
			var items = new List<object>();
			if (connectionEndpoint) {
				items.Add(InvocationConnectionMetadata.Instance);
			}
			if (hubEndpoint) {
				items.Add(new HubMetadata(typeof(DummyHub)));
			}

			context.Features.Set<IEndpointFeature>(new TestEndpointFeature(
				new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(items), "test")));
		}

		return context;
	}

	private static async Task<HttpContext> InvokeAsync(HttpContext context) {
		var reached = false;
		var middleware = new ConnectionCredentialMiddleware(_ => { reached = true; return Task.CompletedTask; });

		await middleware.InvokeAsync(context);

		reached.Should().BeTrue("the middleware must always call the next delegate");
		return context;
	}

	// Promotion ——————————————————————————————————————————————————

	[Fact]
	public async Task OnAConnectionEndpoint_TheQueryCredentialIsPromoted() {
		var context = await InvokeAsync(Request(accessTokenQuery: "abc123", connectionEndpoint: true));

		context.Request.Headers.Authorization.ToString().Should().Be("Bearer abc123");
	}

	[Fact]
	public async Task OnASignalRHub_TheQueryCredentialIsPromoted() {
		var context = await InvokeAsync(Request(accessTokenQuery: "abc123", hubEndpoint: true));

		context.Request.Headers.Authorization.ToString().Should().Be("Bearer abc123");
	}

	[Fact]
	public async Task APrefixedCredential_IsPromotedVerbatim() {
		// The prefix is part of the opaque secret its issuer minted and stored; dispatch
		// matches on it, so altering the value here would break scheme selection.
		var context = await InvokeAsync(Request(accessTokenQuery: "st_prod_a1b2c3", connectionEndpoint: true));

		context.Request.Headers.Authorization.ToString().Should().Be("Bearer st_prod_a1b2c3");
	}

	// Scoping ————————————————————————————————————————————————————

	[Fact]
	public async Task OnAnOrdinaryEndpoint_TheQueryCredentialIsIgnored() {
		var context = await InvokeAsync(Request(accessTokenQuery: "abc123"));

		context.Request.Headers.Authorization.ToString().Should()
			.BeEmpty("a query parameter on an ordinary route carries no authority");
	}

	[Fact]
	public async Task WithNoEndpointResolved_TheQueryCredentialIsIgnored() {
		var context = await InvokeAsync(Request(accessTokenQuery: "abc123", anyEndpoint: false));

		context.Request.Headers.Authorization.ToString().Should()
			.BeEmpty("routing has not run, so the endpoint cannot be classified");
	}

	[Fact]
	public async Task AnExistingAuthorizationHeader_IsNotOverwritten() {
		var context = await InvokeAsync(Request(
			authorization: "Bearer from-header",
			accessTokenQuery: "from-query",
			connectionEndpoint: true));

		context.Request.Headers.Authorization.ToString().Should().Be("Bearer from-header");
	}

	[Fact]
	public async Task AnAuthorizationHeaderOfAnotherScheme_IsNotOverwritten() {
		var context = await InvokeAsync(Request(
			authorization: "ApiKey abc123",
			accessTokenQuery: "from-query",
			connectionEndpoint: true));

		context.Request.Headers.Authorization.ToString().Should()
			.Be("ApiKey abc123", "an explicit header is the caller's choice, whatever scheme it names");
	}

	[Fact]
	public async Task AnEmptyQueryCredential_PromotesNothing() {
		var context = await InvokeAsync(Request(accessTokenQuery: "", connectionEndpoint: true));

		context.Request.Headers.Authorization.ToString().Should().BeEmpty();
	}

	[Fact]
	public async Task NoQueryCredential_PromotesNothing() {
		var context = await InvokeAsync(Request(connectionEndpoint: true));

		context.Request.Headers.Authorization.ToString().Should().BeEmpty();
	}

}
