namespace Cirreum.Services.Server.Tests;

using Cirreum.Authentication;
using Cirreum.Http.Invocation;
using Cirreum.Invocation;
using Cirreum.RemoteServices;
using Cirreum.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

public class UserStateAccessorTests {

	private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
		new(new ClaimsIdentity(claims, "TestAuth"));

	private static (UserStateAccessor Accessor, DefaultHttpContext Http) CreateAccessor(
		ClaimsPrincipal principal,
		Action<IServiceCollection>? configureServices = null,
		string? appNameHeader = null,
		string? authenticatedScheme = null,
		string? originScheme = null) {

		var services = new ServiceCollection();
		configureServices?.Invoke(services);

		var http = new DefaultHttpContext {
			User = principal,
			RequestServices = services.BuildServiceProvider()
		};
		if (appNameHeader is not null) {
			http.Request.Headers[RemoteIdentityConstants.AppNameHeader] = appNameHeader;
		}
		if (authenticatedScheme is not null) {
			http.Items[AuthenticationContextKeys.AuthenticatedScheme] = authenticatedScheme;
		}
		if (originScheme is not null) {
			http.Items[AuthenticationContextKeys.OriginScheme] = originScheme;
		}

		var invocationAccessor = Substitute.For<IInvocationContextAccessor>();
		invocationAccessor.Current.Returns(new HttpInvocationContext(http));

		var environment = Substitute.For<IWebHostEnvironment>();
		environment.EnvironmentName.Returns("Production");

		return (new UserStateAccessor(invocationAccessor, environment), http);
	}

	private static ISchemeClaimAuthorityMap CreateMap(string scheme, SubjectKind kind) {
		var map = Substitute.For<ISchemeClaimAuthorityMap>();
		map.Get(Arg.Any<string?>()).Returns(SchemeClaimAuthority.Undeclared);
		map.Get(scheme).Returns(new SchemeClaimAuthority(kind, default, default));
		return map;
	}

	// Step 0 — subject kind
	// -------------------------------------------------------------

	[Fact]
	public async Task SubjectKind_ResolvesFromTheEffectiveScheme_OriginWins() {
		var map = CreateMap("descope", SubjectKind.Human);
		var (accessor, _) = CreateAccessor(
			CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "user-1")),
			services => services.AddSingleton(map),
			authenticatedScheme: "SessionTicket:Bearer",
			originScheme: "descope");

		var user = await accessor.GetUserState();

		user.SubjectKind.Should().Be(SubjectKind.Human);
		map.Received(1).Get("descope");
	}

	[Fact]
	public async Task SubjectKind_ResolvesFromTheAuthenticatedScheme_WhenNoOrigin() {
		var map = CreateMap("ApiKey:Header", SubjectKind.Machine);
		var (accessor, _) = CreateAccessor(
			CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "client-1")),
			services => services.AddSingleton(map),
			authenticatedScheme: "ApiKey:Header");

		var user = await accessor.GetUserState();

		user.SubjectKind.Should().Be(SubjectKind.Machine);
	}

	[Fact]
	public async Task SubjectKind_IsUnknown_WhenNoMapIsRegistered() {
		var (accessor, _) = CreateAccessor(
			CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "user-1")),
			authenticatedScheme: "descope");

		var user = await accessor.GetUserState();

		user.SubjectKind.Should().Be(SubjectKind.Unknown);
	}

	// App-name fallback — machine gate + fill-only
	// -------------------------------------------------------------

	[Fact]
	public async Task AppNameFallback_NamesANamelessMachineCaller() {
		var map = CreateMap("ApiKey:Header", SubjectKind.Machine);
		var (accessor, http) = CreateAccessor(
			CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "client-1")),
			services => services.AddSingleton(map),
			appNameHeader: "Solvaeon.Portal",
			authenticatedScheme: "ApiKey:Header");

		await accessor.GetUserState();

		var identity = (ClaimsIdentity)http.User.Identity!;
		identity.FindFirst(ClaimTypes.Name)?.Value.Should().Be("Solvaeon.Portal");
	}

	[Fact]
	public async Task AppNameFallback_NeverNamesAHumanSubject_WhenTheMapIsRegistered() {
		var map = CreateMap("descope", SubjectKind.Human);
		var (accessor, http) = CreateAccessor(
			CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "user-1")),
			services => services.AddSingleton(map),
			appNameHeader: "Solvaeon.Portal",
			authenticatedScheme: "descope");

		await accessor.GetUserState();

		var identity = (ClaimsIdentity)http.User.Identity!;
		identity.FindFirst(ClaimTypes.Name).Should().BeNull();
	}

	[Fact]
	public async Task AppNameFallback_KeepsTheLegacyBlankNameGate_WithoutAMap() {
		var (accessor, http) = CreateAccessor(
			CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "client-1")),
			appNameHeader: "Solvaeon.Portal",
			authenticatedScheme: "ApiKey:Header");

		await accessor.GetUserState();

		var identity = (ClaimsIdentity)http.User.Identity!;
		identity.FindFirst(ClaimTypes.Name)?.Value.Should().Be("Solvaeon.Portal");
	}

	[Fact]
	public async Task AppNameFallback_IsFillOnly_AnExistingNameIsNeverDisplaced() {
		var map = CreateMap("ApiKey:Header", SubjectKind.Machine);
		var (accessor, http) = CreateAccessor(
			CreatePrincipal(
				new Claim(ClaimTypes.NameIdentifier, "client-1"),
				new Claim(ClaimTypes.Name, "Credential Client Name")),
			services => services.AddSingleton(map),
			appNameHeader: "Solvaeon.Portal",
			authenticatedScheme: "ApiKey:Header");

		await accessor.GetUserState();

		var identity = (ClaimsIdentity)http.User.Identity!;
		var nameClaims = identity.FindAll(ClaimTypes.Name).ToList();
		nameClaims.Should().ContainSingle().Which.Value.Should().Be("Credential Client Name");
	}

	// Effective-scheme dispatch
	// -------------------------------------------------------------

	[Fact]
	public async Task ApplicationUserResolution_DispatchesOnTheOriginScheme() {
		var originResolver = Substitute.For<IApplicationUserResolver>();
		originResolver.Scheme.Returns("descope");
		originResolver.ResolveAsync(Arg.Any<string>())
			.Returns(Substitute.For<IApplicationUser>());

		var transportResolver = Substitute.For<IApplicationUserResolver>();
		transportResolver.Scheme.Returns("SessionTicket:Bearer");

		var (accessor, _) = CreateAccessor(
			CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "user-1")),
			services => {
				services.AddSingleton(originResolver);
				services.AddSingleton(transportResolver);
			},
			authenticatedScheme: "SessionTicket:Bearer",
			originScheme: "descope");

		var user = await accessor.GetUserState();

		await originResolver.Received(1).ResolveAsync(Arg.Any<string>());
		await transportResolver.DidNotReceive().ResolveAsync(Arg.Any<string>());
		user.ApplicationUser.Should().NotBeNull();
	}

	[Fact]
	public async Task ApplicationUserResolution_DispatchesOnTheAuthenticatedScheme_WhenNoOrigin() {
		var resolver = Substitute.For<IApplicationUserResolver>();
		resolver.Scheme.Returns("descope");
		resolver.ResolveAsync(Arg.Any<string>())
			.Returns(Substitute.For<IApplicationUser>());

		var (accessor, _) = CreateAccessor(
			CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "user-1")),
			services => services.AddSingleton(resolver),
			authenticatedScheme: "descope");

		await accessor.GetUserState();

		await resolver.Received(1).ResolveAsync(Arg.Any<string>());
	}

}
