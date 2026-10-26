namespace Cirreum.Services.Server.Tests;

using Cirreum.Authentication;
using Cirreum.Invocation;
using Cirreum.Invocation.Host;
using Cirreum.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Claims;

public class HostInvocationFactoryTests {

	private sealed class ScopedMarker : IDisposable {
		public bool Disposed { get; private set; }
		public void Dispose() => this.Disposed = true;
	}

	private static (HostInvocationFactory Factory, InvocationContextAccessor Accessor, CancellationTokenSource Stopping) CreateFactory(
		IReadOnlyList<string>? defaultRoles = null,
		Action<IServiceCollection>? configureServices = null,
		string appName = "Test.Host") {

		var services = new ServiceCollection();
		services.AddScoped<ScopedMarker>();
		configureServices?.Invoke(services);
		var provider = services.BuildServiceProvider();

		var stopping = new CancellationTokenSource();
		var lifetime = Substitute.For<IHostApplicationLifetime>();
		lifetime.ApplicationStopping.Returns(stopping.Token);

		var environment = Substitute.For<IHostEnvironment>();
		environment.ApplicationName.Returns(appName);

		var accessor = new InvocationContextAccessor();
		var options = Microsoft.Extensions.Options.Options.Create(new HostInvocationFactoryOptions {
			DefaultRoles = defaultRoles ?? []
		});

		return (new HostInvocationFactory(accessor, provider.GetRequiredService<IServiceScopeFactory>(), lifetime, environment, options), accessor, stopping);
	}

	private static ClaimsPrincipal AuthenticatedUser(params Claim[] claims) =>
		new(new ClaimsIdentity(claims, "Cirreum.Entra"));

	// Lifecycle
	// -------------------------------------------------------------

	[Fact]
	public async Task Begin_SetsCurrent_AndDisposeClearsIt() {
		var (factory, accessor, _) = CreateFactory();

		var invocation = factory.Begin();
		accessor.Current.Should().BeSameAs(invocation.Context);

		await invocation.DisposeAsync();
		accessor.Current.Should().BeNull();
	}

	[Fact]
	public async Task Begin_CreatesAScope_ThatDisposeReleases() {
		var (factory, _, _) = CreateFactory();

		var invocation = factory.Begin();
		var marker = invocation.Context.Services.GetRequiredService<ScopedMarker>();
		marker.Disposed.Should().BeFalse();

		await invocation.DisposeAsync();
		marker.Disposed.Should().BeTrue();
	}

	[Fact]
	public async Task Begin_Throws_WhenAnInvocationIsCurrent() {
		var (factory, _, _) = CreateFactory();
		await using var outer = factory.Begin();

		var act = () => factory.Begin();

		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*SuppressFlow*")
			.WithMessage("*StartDetached*");
	}

	[Fact]
	public async Task Begin_InsideStartDetached_DoesNotNest() {
		var (factory, accessor, _) = CreateFactory();
		await using var outer = factory.Begin();
		IInvocationContext? inner = null;

		await factory.StartDetached(ctx => { inner = ctx; return Task.CompletedTask; });

		inner.Should().NotBeNull().And.NotBeSameAs(outer.Context);
		accessor.Current.Should().BeSameAs(outer.Context);
	}

	[Fact]
	public async Task Aborted_FiresOnApplicationStopping() {
		var (factory, _, stopping) = CreateFactory();
		await using var invocation = factory.Begin();

		invocation.Context.Aborted.IsCancellationRequested.Should().BeFalse();
		await stopping.CancelAsync();
		invocation.Context.Aborted.IsCancellationRequested.Should().BeTrue();
	}

	[Fact]
	public async Task Aborted_FiresOnCallerToken() {
		var (factory, _, _) = CreateFactory();
		using var caller = new CancellationTokenSource();
		await using var invocation = factory.Begin(new() { CancellationToken = caller.Token });

		await caller.CancelAsync();
		invocation.Context.Aborted.IsCancellationRequested.Should().BeTrue();
	}

	[Fact]
	public async Task Context_HasNoConnection_AndDefaultsToHostSource() {
		var (factory, _, _) = CreateFactory();
		await using var invocation = factory.Begin();

		invocation.Context.Connection.Should().BeNull();
		invocation.Context.InvocationSource.Should().Be(InvocationSources.Host);
	}

	[Fact]
	public async Task Context_UsesSuppliedSource() {
		var (factory, _, _) = CreateFactory();
		await using var invocation = factory.Begin(new() { Source = InvocationSources.Queue });

		invocation.Context.InvocationSource.Should().Be(InvocationSources.Queue);
	}

	// Host principal
	// -------------------------------------------------------------

	[Fact]
	public async Task HostPrincipal_NamesTheApplication_OnTheHostScheme() {
		var (factory, _, _) = CreateFactory(appName: "LapCast.Api");
		await using var invocation = factory.Begin();

		var user = invocation.Context.User;
		user.Identity!.IsAuthenticated.Should().BeTrue();
		user.Identity.AuthenticationType.Should().Be(HostInvocationDefaults.Scheme);
		ClaimsHelper.ResolveId(user).Should().Be("LapCast.Api");
		ClaimsHelper.ResolveName(user).Should().Be("LapCast.Api");
		invocation.Context.AuthenticatedScheme.Should().Be(HostInvocationDefaults.Scheme);
		invocation.Context.OriginScheme.Should().BeNull();
	}

	[Fact]
	public async Task HostPrincipal_IsMintedPerInvocation() {
		var (factory, _, _) = CreateFactory();
		ClaimsPrincipal first;
		await using (var one = factory.Begin()) {
			first = one.Context.User;
		}
		await using var two = factory.Begin();

		two.Context.User.Should().NotBeSameAs(first);
	}

	[Fact]
	public async Task HostPrincipal_MintsNoRoles_ByDefault() {
		var (factory, _, _) = CreateFactory();
		await using var invocation = factory.Begin();

		ClaimsHelper.ResolveRoles(invocation.Context.User).Should().BeEmpty();
	}

	[Fact]
	public async Task HostPrincipal_MintsConfiguredDefaultRoles_WhenCallSuppliesNull() {
		var (factory, _, _) = CreateFactory(defaultRoles: ["System"]);
		await using var invocation = factory.Begin();

		invocation.Context.User.IsInRole("System").Should().BeTrue();
	}

	[Fact]
	public async Task HostPrincipal_CallRolesReplaceDefaults() {
		var (factory, _, _) = CreateFactory(defaultRoles: ["System"]);
		await using var invocation = factory.Begin(new() { Roles = ["Importer"] });

		invocation.Context.User.IsInRole("Importer").Should().BeTrue();
		invocation.Context.User.IsInRole("System").Should().BeFalse();
	}

	[Fact]
	public async Task HostPrincipal_EmptyCallRoles_MintNone_EvenWithDefaults() {
		var (factory, _, _) = CreateFactory(defaultRoles: ["System"]);
		await using var invocation = factory.Begin(new() { Roles = [] });

		ClaimsHelper.ResolveRoles(invocation.Context.User).Should().BeEmpty();
	}

	// Continuation principal
	// -------------------------------------------------------------

	[Fact]
	public async Task Continuation_StampsOriginScheme_AndKeepsHostAsAuthenticatedScheme() {
		var (factory, _, _) = CreateFactory();
		var user = AuthenticatedUser(new Claim("sub", "u-1"), new Claim("name", "Ada"));
		await using var invocation = factory.Begin(new() { User = user, OriginScheme = "Cirreum.Entra" });

		invocation.Context.AuthenticatedScheme.Should().Be(HostInvocationDefaults.Scheme);
		invocation.Context.OriginScheme.Should().Be("Cirreum.Entra");
		invocation.Context.EffectiveScheme.Should().Be("Cirreum.Entra");
		ClaimsHelper.ResolveId(invocation.Context.User).Should().Be("u-1");
		ClaimsHelper.ResolveName(invocation.Context.User).Should().Be("Ada");
	}

	[Fact]
	public async Task Continuation_NeverModifiesTheCallersPrincipal() {
		var (factory, _, _) = CreateFactory();
		var user = AuthenticatedUser(new Claim("sub", "u-1"), new Claim(ClaimTypes.Role, "Reader"));
		var originalClaimCount = user.Claims.Count();
		await using var invocation = factory.Begin(new() { User = user, Roles = ["Importer"] });

		invocation.Context.User.Should().NotBeSameAs(user);
		invocation.Context.User.IsInRole("Importer").Should().BeTrue();
		invocation.Context.User.IsInRole("Reader").Should().BeTrue();
		user.IsInRole("Importer").Should().BeFalse();
		user.Claims.Count().Should().Be(originalClaimCount);
	}

	[Fact]
	public async Task Continuation_KeepsTheCallersIdentityPrimary() {
		var (factory, _, _) = CreateFactory();
		var user = AuthenticatedUser(new Claim("sub", "u-1"), new Claim("name", "Ada"));
		await using var invocation = factory.Begin(new() { User = user, Roles = ["Importer"] });

		invocation.Context.User.Identity!.AuthenticationType.Should().Be("Cirreum.Entra");
		invocation.Context.User.Identities.Should().HaveCount(2);
	}

	[Fact]
	public void Continuation_RejectsAnUnauthenticatedPrincipal() {
		var (factory, _, _) = CreateFactory();
		var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

		var act = () => factory.Begin(new() { User = anonymous });

		act.Should().Throw<InvalidOperationException>().WithMessage("*authenticated*");
	}

	// Through UserStateAccessor
	// -------------------------------------------------------------

	[Fact]
	public async Task UserState_ResolvesMachine_WithTheApplicationName() {
		var map = Substitute.For<ISchemeClaimAuthorityMap>();
		map.Get(Arg.Any<string?>()).Returns(SchemeClaimAuthority.Undeclared);
		map.Get(HostInvocationDefaults.Scheme).Returns(new SchemeClaimAuthority(SubjectKind.Machine, default, default));
		var (factory, accessor, _) = CreateFactory(
			appName: "LapCast.Api",
			configureServices: s => s.AddSingleton(map));
		var environment = Substitute.For<IWebHostEnvironment>();
		environment.EnvironmentName.Returns("Production");
		await using var invocation = factory.Begin(new() { Roles = ["System"] });

		var user = await new UserStateAccessor(accessor, environment).GetUserState();

		user.IsAuthenticated.Should().BeTrue();
		user.SubjectKind.Should().Be(SubjectKind.Machine);
		user.Id.Should().Be("LapCast.Api");
		user.Should().BeOfType<ServerUserState>().Which.AppName.Should().Be("LapCast.Api");
		user.ApplicationUser.Should().BeNull();
		user.Principal.IsInRole("System").Should().BeTrue();
	}

	[Fact]
	public async Task UserState_ForAContinuation_FollowsTheSubject() {
		var map = Substitute.For<ISchemeClaimAuthorityMap>();
		map.Get(Arg.Any<string?>()).Returns(SchemeClaimAuthority.Undeclared);
		map.Get("Cirreum.Entra").Returns(new SchemeClaimAuthority(SubjectKind.Human, default, default));
		var (factory, accessor, _) = CreateFactory(configureServices: s => s.AddSingleton(map));
		var environment = Substitute.For<IWebHostEnvironment>();
		environment.EnvironmentName.Returns("Production");
		var caller = AuthenticatedUser(new Claim("sub", "u-1"), new Claim("name", "Ada"));
		await using var invocation = factory.Begin(new() { User = caller, OriginScheme = "Cirreum.Entra" });

		var user = await new UserStateAccessor(accessor, environment).GetUserState();

		user.SubjectKind.Should().Be(SubjectKind.Human);
		user.Id.Should().Be("u-1");
	}

	// Registration
	// -------------------------------------------------------------

	[Fact]
	public void AddCoreServices_RegistersTheFactory_AndDeclaresTheHostScheme() {
		var services = new ServiceCollection();

		services.AddCoreServices();

		services.Should().Contain(d => d.ServiceType == typeof(IHostInvocationFactory));
		services
			.Select(d => d.ImplementationInstance)
			.OfType<SchemeClaimAuthorityRegistration>()
			.Should().ContainSingle(r => r.Scheme == HostInvocationDefaults.Scheme && r.SubjectKind == SubjectKind.Machine);
	}

}
